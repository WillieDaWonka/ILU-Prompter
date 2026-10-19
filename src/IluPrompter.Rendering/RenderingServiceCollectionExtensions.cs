using IluPrompter.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Rendering;

/// <summary>Stream C owns this file.</summary>
public static class RenderingServiceCollectionExtensions
{
    public static IServiceCollection AddIluRendering(this IServiceCollection services)
    {
        services.AddSingleton<IMonitorService, MonitorService>();
        services.AddSingleton<IOutputSurface, OutputHost>();
        services.AddSingleton<PreviewServices>();
        return services;
    }
}
