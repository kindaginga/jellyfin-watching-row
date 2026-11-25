using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WatchingRow;

/// <summary>
/// Shows partly watched items and each series' next episode in the Continue Watching row.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="serializer">The XML serializer.</param>
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
        : base(paths, serializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the loaded plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Watching Row";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("5b1f0c3e-7d2a-4c86-9a41-2e6f8d0b9c17");

    /// <inheritdoc />
    public override string Description => "Shows partly watched items and each series' next episode in one Continue Watching row.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo { Name = Name, EmbeddedResourcePath = GetType().Namespace + ".configPage.html" };
    }
}
