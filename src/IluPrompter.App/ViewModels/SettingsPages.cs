using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;
using IluPrompter.Theme;

namespace IluPrompter.App.ViewModels;

/// <summary>A Settings page that can go back to its defaults (with a confirmation).</summary>
public interface ISettingsPage
{
    string Title { get; }

    bool CanReset { get; }

    /// <summary>One sentence for the confirmation dialog: what the reset does.</summary>
    string ResetDescription { get; }

    void ResetToDefaults();
}

/// <summary>Settings › Output: display picker, margin editor, lines, spacing, reading line and the output toggles.</summary>
public sealed class OutputPageViewModel : SettingsBackedViewModel, ISettingsPage
{
    public const string AutoDisplay = "";

    private readonly IMonitorService _monitors;
    private bool _rebuilding;

    public OutputPageViewModel(ISettingsService settings, IUiThread ui, IMonitorService monitors, OutputLayoutViewModel layout)
        : base(settings, ui)
    {
        _monitors = monitors;
        Layout = layout;
        monitors.MonitorsChanged += (_, _) => Ui.Post(RefreshDisplays);
        RefreshDisplays();
    }

    public string Title => "Output";

    public bool CanReset => true;

    public string ResetDescription => "The display, margins, lines, spacing and reading line go back to their factory values.";

    public OutputLayoutViewModel Layout { get; }

    public ObservableCollection<DeviceChoice> Displays { get; } = [];

    public string SelectedDisplayId
    {
        get => Current.Output.MonitorStableId ?? AutoDisplay;
        set
        {
            if (value is null || _rebuilding) return;
            var id = value.Length == 0 ? null : value;
            if (id == Current.Output.MonitorStableId) return;
            Update(s => s with { Output = s.Output with { MonitorStableId = id } });
        }
    }

    public string DisplayHint => Displays.Count <= 1
        ? "Connect the presenter's display, then press F11. The output never opens on this screen."
        : "The output opens on this display and never on the screen you are using.";

    public void RefreshDisplays()
    {
        _rebuilding = true;
        Displays.Clear();
        Displays.Add(new DeviceChoice(AutoDisplay, "Automatic: the first extra display"));
        foreach (var m in _monitors.GetMonitors().Where(m => !m.IsPrimary))
            Displays.Add(new DeviceChoice(m.StableId, $"{m.Label} · {m.BoundsPx.Width} × {m.BoundsPx.Height}"));
        // A saved display that is not connected right now stays selectable so the choice is not lost.
        var saved = Current.Output.MonitorStableId;
        if (saved is not null && Displays.All(d => d.Id != saved)) Displays.Add(new DeviceChoice(saved, "Saved display (not connected)"));
        _rebuilding = false;
        OnPropertyChanged(nameof(SelectedDisplayId));
        OnPropertyChanged(nameof(DisplayHint));
    }

    public void ResetToDefaults()
    {
        Update(s => s with { Output = s.Output with { MonitorStableId = null } });
        Layout.ResetToDefaults();
        RefreshDisplays();
    }

    protected override void OnSettingsReloaded() => RefreshDisplays();
}

/// <summary>An ink in the default-ink picker.</summary>
public sealed record InkChoice(string Token, string Name, Brush Swatch);

/// <summary>Settings › Text: output face, weight, default ink, alignment and the editor zoom default.</summary>
public sealed class TextPageViewModel : SettingsBackedViewModel, ISettingsPage
{
    public const string OtherFace = "Other";

    public static IReadOnlyList<string> MenuFaces { get; } = ["Poppins", "Segoe UI", "Bahnschrift", "Arial"];

    private string _otherFamily = "Calibri";

    public TextPageViewModel(ISettingsService settings, IUiThread ui)
        : base(settings, ui)
    {
        Inks = ThemeResources.Inks.Select(i => new InkChoice(i.Token, i.Name + (i.Token == "ink-white" ? " (default)" : ""), ThemeResources.GetBrush(i.Token))).ToArray();
        if (!MenuFaces.Contains(Current.Text.FontFace, StringComparer.OrdinalIgnoreCase)) _otherFamily = Current.Text.FontFace;
    }

    public string Title => "Text";

    public bool CanReset => true;

    public string ResetDescription => "The face, weight, ink, alignment and editor zoom go back to their factory values.";

    public IReadOnlyList<string> Faces { get; } = [.. MenuFaces, OtherFace];

    public IReadOnlyList<InkChoice> Inks { get; }

    /// <summary>Every installed font family, for "Other installed font…".</summary>
    public IReadOnlyList<string> InstalledFamilies { get; } =
        Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public string SelectedFace
    {
        get => MenuFaces.Contains(Current.Text.FontFace, StringComparer.OrdinalIgnoreCase)
            ? MenuFaces.First(f => string.Equals(f, Current.Text.FontFace, StringComparison.OrdinalIgnoreCase))
            : OtherFace;
        set
        {
            if (value is null || value == SelectedFace) return;
            SetFace(value == OtherFace ? _otherFamily : value);
        }
    }

    public bool IsOtherFace => SelectedFace == OtherFace;

    public string OtherFamily
    {
        get => _otherFamily;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == _otherFamily) return;
            _otherFamily = value;
            if (IsOtherFace) SetFace(value);
            OnPropertyChanged();
        }
    }

    public string OtherWarning => "▲ Check bold and italic on the output";

    private void SetFace(string face)
    {
        if (string.Equals(face, Current.Text.FontFace, StringComparison.Ordinal)) return;
        Update(s => s with { Text = s.Text with { FontFace = face } });
    }

    public bool IsRegularWeight { get => Current.Text.FontWeight == 400; set => SetWeight(value, 400); }

    public bool IsMediumWeight { get => Current.Text.FontWeight != 400; set => SetWeight(value, 500); }

    private void SetWeight(bool on, int weight)
    {
        if (!on || Current.Text.FontWeight == weight) return;
        Update(s => s with { Text = s.Text with { FontWeight = weight } });
    }

    public string DefaultInk
    {
        get => Current.Text.DefaultInk;
        set
        {
            if (string.IsNullOrEmpty(value) || value == Current.Text.DefaultInk) return;
            Update(s => s with { Text = s.Text with { DefaultInk = value } });
        }
    }

    public bool IsAlignedLeft { get => Current.Output.Alignment == TextAlignmentSetting.Left; set => SetAlignment(value, TextAlignmentSetting.Left); }

    public bool IsAlignedCentre { get => Current.Output.Alignment == TextAlignmentSetting.Centre; set => SetAlignment(value, TextAlignmentSetting.Centre); }

    private void SetAlignment(bool on, TextAlignmentSetting alignment)
    {
        if (!on || Current.Output.Alignment == alignment) return;
        Update(s => s with { Output = s.Output with { Alignment = alignment } });
    }

    /// <summary>Editor zoom default, 70 to 200 %.</summary>
    public double EditorZoom
    {
        get => Current.Editor.ZoomPercent;
        set
        {
            var v = Math.Round(Clamp(value, 70, 200));
            if ((int)v == Current.Editor.ZoomPercent) return;
            Update(s => s with { Editor = s.Editor with { ZoomPercent = (int)v } });
        }
    }

    public void ResetToDefaults()
    {
        var d = PrompterSettings.Default;
        _otherFamily = "Calibri";
        Update(s => s with
        {
            Text = s.Text with { FontFace = d.Text.FontFace, FontWeight = d.Text.FontWeight, DefaultInk = d.Text.DefaultInk },
            Output = s.Output with { Alignment = d.Output.Alignment },
            Editor = s.Editor with { ZoomPercent = d.Editor.ZoomPercent },
        });
    }
}

/// <summary>Settings › Scrolling: default mode, speed step, start speed, acceleration, pause at sections.</summary>
public sealed class ScrollingPageViewModel : SettingsBackedViewModel, ISettingsPage
{
    public ScrollingPageViewModel(ISettingsService settings, IUiThread ui)
        : base(settings, ui)
    {
    }

    public string Title => "Scrolling";

    public bool CanReset => true;

    public string ResetDescription => "The default mode, speeds, acceleration and section pausing go back to their factory values.";

    public bool DefaultIsDisplay { get => Current.Scroll.DefaultMode == ScrollMode.Display; set => SetMode(value, ScrollMode.Display); }

    public bool DefaultIsLine { get => Current.Scroll.DefaultMode == ScrollMode.Line; set => SetMode(value, ScrollMode.Line); }

    private void SetMode(bool on, ScrollMode mode)
    {
        if (!on || Current.Scroll.DefaultMode == mode) return;
        Update(s => s with { Scroll = s.Scroll with { DefaultMode = mode } });
    }

    /// <summary>The − and + step, 0.1 to 2.0.</summary>
    public double SpeedStep
    {
        get => Current.Scroll.SpeedStep;
        set
        {
            var v = Math.Round(Clamp(value, 0.1, 2.0), 1);
            if (Math.Abs(v - Current.Scroll.SpeedStep) < 0.0001) return;
            Update(s => s with { Scroll = s.Scroll with { SpeedStep = v } });
        }
    }

    public double StartSpeed
    {
        get => Current.Scroll.StartSpeed;
        set
        {
            var v = Math.Round(Clamp(value, 0.5, 10), 1);
            if (Math.Abs(v - Current.Scroll.StartSpeed) < 0.0001) return;
            Update(s => s with { Scroll = s.Scroll with { StartSpeed = v } });
        }
    }

    public bool IsGentle { get => Current.Scroll.Acceleration == Acceleration.Gentle; set => SetAcceleration(value, Acceleration.Gentle); }

    public bool IsNormal { get => Current.Scroll.Acceleration == Acceleration.Normal; set => SetAcceleration(value, Acceleration.Normal); }

    public bool IsQuick { get => Current.Scroll.Acceleration == Acceleration.Quick; set => SetAcceleration(value, Acceleration.Quick); }

    private void SetAcceleration(bool on, Acceleration acceleration)
    {
        if (!on || Current.Scroll.Acceleration == acceleration) return;
        Update(s => s with { Scroll = s.Scroll with { Acceleration = acceleration } });
    }

    public bool PauseAtSections
    {
        get => Current.Scroll.PauseAtSections;
        set
        {
            if (value == Current.Scroll.PauseAtSections) return;
            Update(s => s with { Scroll = s.Scroll with { PauseAtSections = value } });
        }
    }

    public void ResetToDefaults()
    {
        var d = PrompterSettings.Default.Scroll;
        Update(s => s with
        {
            Scroll = s.Scroll with
            {
                DefaultMode = d.DefaultMode,
                SpeedStep = d.SpeedStep,
                StartSpeed = d.StartSpeed,
                Acceleration = d.Acceleration,
                PauseAtSections = d.PauseAtSections,
            },
        });
    }
}

/// <summary>Settings › Shortcuts: the read-only key map.</summary>
public sealed class ShortcutsPageViewModel : ISettingsPage
{
    public string Title => "Shortcuts";

    public bool CanReset => false;

    public string ResetDescription => "";

    public IReadOnlyList<ShortcutGroup> Groups => ShortcutCatalog.Groups;

    public string ClickerNote => ShortcutCatalog.ClickerNote;

    public void ResetToDefaults()
    {
    }
}

/// <summary>Settings › About: logo, version and licences.</summary>
public sealed class AboutPageViewModel : ISettingsPage
{
    public string Title => "About";

    public bool CanReset => false;

    public string ResetDescription => "";

    public Uri LogoUri => ThemeResources.LogoUri;

    public string Version
    {
        get
        {
            var assembly = typeof(AboutPageViewModel).Assembly;
            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var text = info ?? assembly.GetName().Version?.ToString() ?? "0.1.0";
            var plus = text.IndexOf('+');
            return "Version " + (plus >= 0 ? text[..plus] : text);
        }
    }

    public string Tagline => "A teleprompter for the ILUMINA venue.";

    public IReadOnlyList<string> Licences { get; } =
    [
        "Poppins is licensed under the SIL Open Font License 1.1.",
        "DM Mono is licensed under the SIL Open Font License 1.1.",
        "The licence texts are in the Licences folder next to the app.",
    ];

    public void ResetToDefaults()
    {
    }
}

/// <summary>A row in the Settings navigation.</summary>
public sealed record SettingsPageItem(SettingsPageId Id, ISettingsPage Page)
{
    public string Title => Page.Title;
}

/// <summary>The Settings window: a navigation list and the page on show. Changes apply as you make them.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private SettingsPageItem _selectedPage;

    public SettingsViewModel(
        ControllerPageViewModel controller,
        OutputPageViewModel output,
        TextPageViewModel text,
        ScrollingPageViewModel scrolling,
        ShortcutsPageViewModel shortcuts,
        AboutPageViewModel about,
        IDialogService dialogs)
    {
        _dialogs = dialogs;
        Controller = controller;
        Pages =
        [
            new SettingsPageItem(SettingsPageId.Controller, controller),
            new SettingsPageItem(SettingsPageId.Output, output),
            new SettingsPageItem(SettingsPageId.Text, text),
            new SettingsPageItem(SettingsPageId.Scrolling, scrolling),
            new SettingsPageItem(SettingsPageId.Shortcuts, shortcuts),
            new SettingsPageItem(SettingsPageId.About, about),
        ];
        _selectedPage = Pages[0];
    }

    public ControllerPageViewModel Controller { get; }

    public IReadOnlyList<SettingsPageItem> Pages { get; }

    public void Select(SettingsPageId id) => SelectedPage = Pages.First(p => p.Id == id);

    public bool CanResetPage => SelectedPage.Page.CanReset;

    partial void OnSelectedPageChanged(SettingsPageItem value)
    {
        OnPropertyChanged(nameof(CanResetPage));
        if (value.Id == SettingsPageId.Controller) Controller.RefreshDevices();
    }

    [RelayCommand]
    private void ResetPage()
    {
        var page = SelectedPage.Page;
        if (!page.CanReset) return;
        var choice = _dialogs.Ask(new DialogRequest(
            "Reset this page to defaults?",
            page.ResetDescription,
            "Reset page",
            "Keep settings",
            PrimaryIsDefault: false));
        if (choice == DialogChoice.Primary) page.ResetToDefaults();
    }

    /// <summary>10 Hz while the window is open.</summary>
    public void Poll() => Controller.Poll();
}
