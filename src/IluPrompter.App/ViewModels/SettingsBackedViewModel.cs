using CommunityToolkit.Mvvm.ComponentModel;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// Base for view models whose properties are read straight from <see cref="ISettingsService.Current"/> and written
/// back with <see cref="ISettingsService.Update"/>. Changes apply as you make them (DESIGN.md: no OK or Apply).
/// When settings change from anywhere, every binding is re-read.
/// </summary>
public abstract class SettingsBackedViewModel : ObservableObject
{
    protected SettingsBackedViewModel(ISettingsService settings, IUiThread ui)
    {
        Settings = settings;
        Ui = ui;
        settings.Changed += OnSettingsChanged;
    }

    protected ISettingsService Settings { get; }

    protected IUiThread Ui { get; }

    protected PrompterSettings Current => Settings.Current;

    /// <summary>Re-read every property (empty name).</summary>
    public void Reload()
    {
        OnPropertyChanged(string.Empty);
        OnSettingsReloaded();
    }

    /// <summary>Hook for pages that cache lists built from settings.</summary>
    protected virtual void OnSettingsReloaded()
    {
    }

    protected void Update(Func<PrompterSettings, PrompterSettings> change) => Settings.Update(change);

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e) => Ui.Post(Reload);

    public static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : Math.Min(max, Math.Max(min, value));
}
