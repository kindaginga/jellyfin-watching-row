using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.WatchingRow;

/// <summary>
/// One candidate for the combined row.
/// </summary>
/// <param name="ItemId">The item to show.</param>
/// <param name="SeriesId">The series the item belongs to, or null for a movie.</param>
/// <param name="LastActivity">When the user last watched this item or its series.</param>
public sealed record RowEntry(Guid ItemId, Guid? SeriesId, DateTime? LastActivity);

/// <summary>
/// Combines partly watched items and each series' next episode into one row, most recent first.
/// </summary>
public static class RowMerger
{
    /// <summary>
    /// Merges the two lists, keeping one entry per series and one per item.
    /// </summary>
    /// <param name="resume">Partly watched items.</param>
    /// <param name="nextUp">Each series' next or partly watched episode.</param>
    /// <returns>The combined row, most recent activity first.</returns>
    public static IReadOnlyList<RowEntry> Merge(IEnumerable<RowEntry> resume, IEnumerable<RowEntry> nextUp)
    {
        var items = new HashSet<Guid>();
        var series = new HashSet<Guid>();
        var row = new List<RowEntry>();
        foreach (var entry in resume.Concat(nextUp))
        {
            // Skips an item already in the row, or a second entry for a series already in the row
            if (!items.Add(entry.ItemId) || (entry.SeriesId is Guid id && !series.Add(id)))
            {
                continue;
            }

            row.Add(entry);
        }

        // Orders by last activity, newest first, keeping the input order for ties
        return row.Select((entry, index) => (entry, index))
            .OrderByDescending(pair => pair.entry.LastActivity ?? DateTime.MinValue)
            .ThenBy(pair => pair.index)
            .Select(pair => pair.entry)
            .ToList();
    }
}
