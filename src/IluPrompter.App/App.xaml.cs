using System.Globalization;
using System.Windows;
using IluPrompter.App.Composition;
using IluPrompter.App.Services;
using IluPrompter.App.Views;
using IluPrompter.Core;
using IluPrompter.Documents;
using IluPrompter.Input;
using IluPrompter.Rendering;
using IluPrompter.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace IluPrompter.App;

/// <summary>Composition root. Stream D owns this file; the startup order below is part of the contract (ARCHITECTURE 2.15).</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var diagnostics = e.Args.Contains("--diag", StringComparer.OrdinalIgnoreCase);
        TextContextMenus.Register();

        var collection = new ServiceCollection()
            .AddIluCore(diagnostics)
            .AddIluDocuments()
            .AddIluInput()
            .AddIluRendering()
            .AddIluApp();

        // Dev/test only: --settings-file <path> keeps the operator's own settings untouched (the last registration wins).
        var settingsFile = TryGetOption(e.Args, "--settings-file");
        if (settingsFile is not null)
        {
            var path = System.IO.Path.GetFullPath(settingsFile);
            collection.AddSingleton<ISettingsService>(sp => new JsonSettingsService(sp.GetRequiredService<IAppLog>(), path));
        }

        _services = collection.BuildServiceProvider();

        var log = _services.GetRequiredService<IAppLog>();
        log.Info("app", "Starting ILU Prompter " + typeof(App).Assembly.GetName().Version);
        if (settingsFile is not null) log.Warn("app", "Dev flag --settings-file: settings are read from and written to " + System.IO.Path.GetFullPath(settingsFile));
        DispatcherUnhandledException += (_, args) =>
        {
            // An editing or layout glitch must not take a live show down: log it and carry on.
            log.Error("app", "Unhandled UI exception", args.Exception);
            args.Handled = true;
        };

        try
        {
            // IScrollEngine reads Scroll.StartSpeed and DefaultMode once in its constructor, so nothing may resolve it
            // (directly or through another service) before settings are loaded. Only IAppLog has been resolved so far.
            // 1. settings  2. output thread  3. main window (+ input bridge + preview)  4. input router  5. open the last script
            _services.GetRequiredService<ISettingsService>().Load();
            await _services.GetRequiredService<IOutputSurface>().InitializeAsync();

            var main = _services.GetRequiredService<MainWindow>();
            MainWindow = main;
            if (TryParseWindowSize(e.Args, out var width, out var height)) main.UseDevWindowSize(width, height);
            main.Show();

            _services.GetRequiredService<IInputRouter>().Start();
            var session = _services.GetRequiredService<ScriptSession>();
            // A script path on the command line (Explorer "Open with", a shortcut) opens that script instead of the last one.
            var scriptPath = FindScriptPath(e.Args);
            if (scriptPath is null || !session.OpenPath(scriptPath)) session.OpenLastOrNew();
        }
        catch (Exception ex)
        {
            log.Error("app", "Startup failed", ex);
            Shutdown(1);
        }
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

    /// <summary>Flags that take a value (the next argument), so the value is never mistaken for a script path.</summary>
    private static readonly string[] OptionsWithValue = ["--window-size", "--settings-file"];

    /// <summary>The value after <paramref name="option"/>, or null.</summary>
    internal static string? TryGetOption(string[] args, string option)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal) ? args[index + 1] : null;
    }

    /// <summary>The first argument that is neither a flag nor a flag's value: a script to open.</summary>
    internal static string? FindScriptPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (OptionsWithValue.Contains(arg, StringComparer.OrdinalIgnoreCase)) i++;
                continue;
            }

            return System.IO.Path.GetFullPath(arg);
        }

        return null;
    }

    /// <summary>Dev only: <c>--window-size 1366x768</c> opens the control window at that exact size (for layout checks).</summary>
    internal static bool TryParseWindowSize(string[] args, out double width, out double height)
    {
        width = height = 0;
        var index = Array.FindIndex(args, a => string.Equals(a, "--window-size", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return false;
        var parts = args[index + 1].Split('x', 'X');
        return parts.Length == 2
               && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out width)
               && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out height)
               && width >= 800 && height >= 500;
    }
}
