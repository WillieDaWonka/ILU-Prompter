using System.Windows;
using IluPrompter.App.Composition;
using IluPrompter.App.Views;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Input;
using IluPrompter.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App;

/// <summary>Composition root. Stream D owns this file; the startup order below is part of the contract.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var diagnostics = e.Args.Contains("--diag", StringComparer.OrdinalIgnoreCase);

        _services = new ServiceCollection()
            .AddIluCore(diagnostics)
            .AddIluDocuments()
            .AddIluInput()
            .AddIluRendering()
            .AddIluApp()
            .BuildServiceProvider();

        var log = _services.GetRequiredService<IAppLog>();
        log.Info("app", "Starting ILU Prompter " + typeof(App).Assembly.GetName().Version);
        DispatcherUnhandledException += (_, args) => log.Error("app", "Unhandled UI exception", args.Exception);

        // 1. settings  2. output thread  3. main window (+ input bridge + preview)  4. input router
        _services.GetRequiredService<ISettingsService>().Load();
        await _services.GetRequiredService<IOutputSurface>().InitializeAsync();

        var main = _services.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();

        _services.GetRequiredService<IInputRouter>().Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            _services.GetRequiredService<IInputRouter>().Stop();
            _services.GetRequiredService<IOutputSurface>().ShutdownAsync().GetAwaiter().GetResult();
            _services.GetRequiredService<ISettingsService>().SaveAsync().GetAwaiter().GetResult();
            _services.Dispose();
        }

        base.OnExit(e);
    }
}
