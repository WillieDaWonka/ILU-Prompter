using IluPrompter.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App.Composition;

/// <summary>Stream D owns this file: windows and view models.</summary>
public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddIluApp(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        return services;
    }
}
