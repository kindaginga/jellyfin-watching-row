using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.WatchingRow;

/// <summary>
/// Answers the Continue Watching request with partly watched items and each series' next episode.
/// </summary>
public partial class ResumeRowMiddleware
{
    private const int DefaultLimit = 24;

    private readonly RequestDelegate _next;
    private readonly ILogger<ResumeRowMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResumeRowMiddleware"/> class.
    /// </summary>
    /// <param name="next">The rest of the request pipeline.</param>
    /// <param name="logger">The logger.</param>
    public ResumeRowMiddleware(RequestDelegate next, ILogger<ResumeRowMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    [GeneratedRegex(@"^/(UserItems/Resume|Users/(?<user>[0-9a-fA-F-]{32,36})/Items/Resume)$", RegexOptions.IgnoreCase)]
    private static partial Regex ResumePath();

    [GeneratedRegex(@"Client=""(?<client>[^""]*)""")]
    private static partial Regex ClientName();

    /// <summary>
    /// Handles one request.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsHomeNextUp(context.Request) && !(Plugin.Instance?.Configuration.KeepNextUpRow ?? false))
        {
            // Answers the home screen's Next Up row with no items, which the home screen hides
            var json = context.RequestServices.GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value.JsonSerializerOptions ?? JsonDefaults.Options;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new QueryResult<BaseItemDto>(0, 0, Array.Empty<BaseItemDto>()), json)).ConfigureAwait(false);
            return;
        }

        var userId = MergeableUser(context.Request);
        if (userId is null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        try
        {
            if (await WriteRowAsync(context, userId.Value).ConfigureAwait(false))
            {
                return;
            }
        }
        catch (Exception error) when (!context.Response.HasStarted)
        {
            _logger.LogError(error, "Watching Row: building the combined row failed, Jellyfin's own row is shown");
        }

        await _next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether a request is the home screen's Next Up row, which alone sends a date cutoff without a series, library or sort order.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>True for the home screen's Next Up row.</returns>
    public static bool IsHomeNextUp(HttpRequest request)
    {
        var query = request.Query;
        return HttpMethods.IsGet(request.Method)
            && string.Equals(request.Path.Value, "/Shows/NextUp", StringComparison.OrdinalIgnoreCase)
            && query.ContainsKey("nextUpDateCutoff")
            && !query.ContainsKey("seriesId") && !query.ContainsKey("parentId") && !query.ContainsKey("sortBy")
            && !query.ContainsKey("startIndex");
    }

    /// <summary>
    /// Returns the user of a Continue Watching request for videos, or null for any other request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The user id, or null.</returns>
    public static Guid? MergeableUser(HttpRequest request)
    {
        var match = ResumePath().Match(request.Path.Value ?? string.Empty);
        if (!HttpMethods.IsGet(request.Method) || !match.Success || request.Query.ContainsKey("parentId")
            || request.Query.ContainsKey("searchTerm") || request.Query.ContainsKey("startIndex"))
        {
            return null;
        }

        var types = request.Query["mediaTypes"].ToString();
        if (types.Length > 0 && !types.Split(',').Any(type => type.Trim().Equals("Video", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var text = match.Groups["user"].Success ? match.Groups["user"].Value : request.Query["userId"].ToString();
        return Guid.TryParse(text, out var id) ? id : null;
    }

    private async Task<bool> WriteRowAsync(HttpContext context, Guid userId)
    {
        var services = context.RequestServices;
        var user = services.GetRequiredService<IUserManager>().GetUserById(userId);
        if (user is null)
        {
            return false;
        }

        var library = services.GetRequiredService<ILibraryManager>();
        var userData = services.GetRequiredService<IUserDataManager>();
        var query = context.Request.Query;
        var limit = int.TryParse(query["limit"], out var asked) && asked > 0 ? asked : DefaultLimit;
        var options = DtoOptionsFrom(query);

        // Reads the partly watched videos, most recently played first
        var resume = library.GetItemList(new InternalItemsQuery(user)
        {
            IsResumable = true,
            Recursive = true,
            MediaTypes = new[] { MediaType.Video },
            OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            Limit = limit,
            DtoOptions = options,
        });

        // Reads each series' next episode, or its partly watched one
        var nextUp = services.GetRequiredService<ITVSeriesManager>().GetNextUp(
            new NextUpQuery { User = user, Limit = limit, EnableResumable = true, EnableRewatching = false },
            options).Items;

        var byId = resume.Concat(nextUp).GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.First());
        var row = RowMerger.Merge(
            resume.Select(item => Entry(item, userData.GetUserData(user, item)?.LastPlayedDate)),
            nextUp.Select(item => Entry(item, LastActivity(library, userData, user, item))))
            .Take(limit)
            .Select(entry => byId[entry.ItemId])
            .ToList();

        var dtos = services.GetRequiredService<IDtoService>().GetBaseItemDtos(row, options, user);
        var result = new QueryResult<BaseItemDto>(0, row.Count, dtos);
        var json = services.GetService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()?.Value.JsonSerializerOptions ?? JsonDefaults.Options;

        _logger.LogInformation(
            "Watching Row: {Count} items ({Resume} partly watched, {NextUp} from Next Up) for {Client}",
            row.Count,
            resume.Count,
            nextUp.Count,
            Uri.UnescapeDataString(ClientName().Match(context.Request.Headers.Authorization.ToString() + context.Request.Headers["X-Emby-Authorization"].ToString()).Groups["client"].Value));

        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(result, json)).ConfigureAwait(false);
        return true;
    }

    private static RowEntry Entry(BaseItem item, DateTime? lastActivity)
    {
        return new RowEntry(item.Id, item is Episode episode && episode.SeriesId != Guid.Empty ? episode.SeriesId : null, lastActivity);
    }

    private static DateTime? LastActivity(ILibraryManager library, IUserDataManager userData, User user, BaseItem item)
    {
        var own = userData.GetUserData(user, item)?.LastPlayedDate;
        if (own is not null || item is not Episode episode || episode.SeriesId == Guid.Empty)
        {
            return own;
        }

        // Uses the date the series was last watched for a next episode that has not been started
        var last = library.GetItemList(new InternalItemsQuery(user)
        {
            AncestorIds = new[] { episode.SeriesId },
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            IsPlayed = true,
            Recursive = true,
            OrderBy = new[] { (ItemSortBy.DatePlayed, SortOrder.Descending) },
            Limit = 1,
        }).FirstOrDefault();
        return last is null ? null : userData.GetUserData(user, last)?.LastPlayedDate;
    }

    private static DtoOptions DtoOptionsFrom(IQueryCollection query)
    {
        var options = new DtoOptions
        {
            EnableImages = !bool.TryParse(query["enableImages"], out var images) || images,
            ImageTypeLimit = int.TryParse(query["imageTypeLimit"], out var typeLimit) ? typeLimit : 1,
            EnableUserData = !bool.TryParse(query["enableUserData"], out var data) || data,
        };
        var fields = query["fields"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<ItemFields>(name, true, out var field) ? field : (ItemFields?)null)
            .OfType<ItemFields>()
            .Append(ItemFields.PrimaryImageAspectRatio)
            .Distinct()
            .ToArray();
        options.Fields = fields;
        var imageTypes = query["enableImageTypes"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<ImageType>(name, true, out var type) ? type : (ImageType?)null)
            .OfType<ImageType>()
            .ToArray();
        if (imageTypes.Length > 0)
        {
            options.ImageTypes = imageTypes;
        }

        return options;
    }
}
