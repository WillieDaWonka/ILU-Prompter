using IluPrompter.Core;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Input;

/// <summary>Stream B owns this file. Providers are registered as IScrollInputProvider (the router receives all).</summary>
public static class InputServiceCollectionExtensions
{
    public static IServiceCollection AddIluInput(this IServiceCollection services)
    {
        services.AddSingleton<IScrollInputProvider, RawMouseWheelProvider>();
        services.AddSingleton<IScrollInputProvider, XInputProvider>();
        services.AddSingleton<IScrollInputProvider, ContourShuttleProvider>();
        services.AddSingleton<IAppInputBridge, AppInputBridge>();
        return services;
    }
}
