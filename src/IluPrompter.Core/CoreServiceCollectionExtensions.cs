using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.Core;

/// <summary>Stream A owns this file. Registers Core services as singletons.</summary>
public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddIluCore(this IServiceCollection services, bool diagnostics)
    {
        services.AddSingleton<IAppLog>(_ => new FileAppLog(diagnostics));
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IPromptDocumentService, PromptDocumentService>();
        services.AddSingleton<IKeyModeState, KeyModeState>();
        services.AddSingleton<IScrollEngine, ScrollEngine>();
        services.AddSingleton<InputRouter>();
        services.AddSingleton<IInputRouter>(sp => sp.GetRequiredService<InputRouter>());
        services.AddSingleton<IInputSink>(sp => sp.GetRequiredService<InputRouter>());
        return services;
    }
}
