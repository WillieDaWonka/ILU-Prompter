using IluPrompter.App.Editing;
using IluPrompter.App.Services;
using IluPrompter.App.ViewModels;
using IluPrompter.App.Views;
using IluPrompter.Core;
using IluPrompter.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App.Composition;

/// <summary>Stream D owns this file: windows, view models and the services that serve them. Everything is a singleton
/// except the Settings window, which is created each time it is opened.</summary>
public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddIluApp(this IServiceCollection services)
    {
        // services
        services.AddSingleton<IUiThread, WpfUiThread>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<OutputController>();
        services.AddSingleton<ScriptSession>();
        services.AddSingleton(sp => new EditorServices(
            sp.GetRequiredService<ISnapshotBuilder>(),
            sp.GetRequiredService<IPromptDocumentService>(),
            sp.GetRequiredService<ISettingsService>(),
            sp.GetRequiredService<IScriptFormatter>(),
            sp.GetRequiredService<IDocumentNavigator>(),
            sp.GetRequiredService<IScriptFileService>(),
            sp.GetRequiredService<IKeyModeState>(),
            sp.GetRequiredService<IScrollEngine>(),
            sp.GetRequiredService<IAppLog>()));

        // view models
        services.AddSingleton<MarginEditorViewModel>();
        services.AddSingleton<OutputLayoutViewModel>();
        services.AddSingleton<LiveViewModel>();
        services.AddSingleton<SectionsViewModel>();
        services.AddSingleton<EditorToolbarViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ControllerPageViewModel>();
        services.AddSingleton<OutputPageViewModel>();
        services.AddSingleton<TextPageViewModel>();
        services.AddSingleton<ScrollingPageViewModel>();
        services.AddSingleton<ShortcutsPageViewModel>();
        services.AddSingleton<AboutPageViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // windows
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<Func<SettingsWindow>>(sp => () => sp.GetRequiredService<SettingsWindow>());
        return services;
    }
}
