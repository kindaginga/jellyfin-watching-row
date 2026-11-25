using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WatchingRow;

/// <summary>
/// The plugin's settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the home screen keeps its separate Next Up row.
    /// </summary>
    public bool KeepNextUpRow { get; set; }
}
