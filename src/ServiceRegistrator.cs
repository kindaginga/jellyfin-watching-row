using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.WatchingRow;

/// <summary>
/// Adds the row handler to Jellyfin's request pipeline.
/// </summary>
public class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<IStartupFilter, RowStartupFilter>();
    }
}

/// <summary>
/// Puts the row handler ahead of Jellyfin's own endpoints.
/// </summary>
public class RowStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public System.Action<IApplicationBuilder> Configure(System.Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            builder.UseMiddleware<ResumeRowMiddleware>();
            next(builder);
        };
    }
}
