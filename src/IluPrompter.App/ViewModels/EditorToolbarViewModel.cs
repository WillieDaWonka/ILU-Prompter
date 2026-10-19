using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using IluPrompter.Documents;
using IluPrompter.Theme;

namespace IluPrompter.App.ViewModels;

/// <summary>An entry in the ink or highlight menu (swatch plus its name).</summary>
public sealed record ColourMenuItem(string Token, string Name, Brush Swatch, string? InkToken);

/// <summary>
/// What the editor toolbar shows: the formatting at the caret, like any word processor (DESIGN.md E).
/// Updated by the EditorController on selection changes; the buttons read it one way and call the controller.
/// </summary>
public sealed partial class EditorToolbarViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> SizeNames = ["Small", "Normal", "Large", "Huge"];

    public static readonly IReadOnlyList<int> ZoomSteps = [70, 85, 100, 125, 150, 175, 200];

    [ObservableProperty]
    private bool _isBold;

    [ObservableProperty]
    private bool _isItalic;

    [ObservableProperty]
    private bool _isUnderline;

    [ObservableProperty]
    private bool _isStrikethrough;

    [ObservableProperty]
    private string _sizeText = "Normal";

    [ObservableProperty]
    private Brush? _inkBar;

    [ObservableProperty]
    private Brush? _highlightBar;

    [ObservableProperty]
    private string _zoomText = "100 %";

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private bool _isFindOpen;

    [ObservableProperty]
    private string _findText = "";

    [ObservableProperty]
    private string _findStatus = "";

    /// <summary>The presenter can see the script now: the editor's left edge carries a 2 px `live` bar.</summary>
    [ObservableProperty]
    private bool _isLive;

    /// <summary>"Changes show on Display 2 as you type" while LIVE.</summary>
    [ObservableProperty]
    private string _liveCaption = "";

    /// <summary>The Wide tier shows Find and zoom in the toolbar; the others fold them into the overflow menu.</summary>
    [ObservableProperty]
    private bool _isWide = true;

    public IReadOnlyList<ColourMenuItem> Inks { get; } =
        ThemeResources.Inks.Select(i => new ColourMenuItem(i.Token, i.Name == "White" ? "White (default)" : i.Name == "Grey" ? "Grey (stage directions)" : i.Name,
            ThemeResources.GetBrush(i.Token), null)).ToArray();

    /// <summary>"No highlight" first, then the five highlights.</summary>
    public IReadOnlyList<ColourMenuItem> Highlights { get; } =
        new[] { new ColourMenuItem("", "No highlight", Brushes.Transparent, null) }
            .Concat(ThemeResources.Highlights.Select(h => new ColourMenuItem(h.Token, h.Name, ThemeResources.GetBrush(h.Token), h.InkToken)))
            .ToArray();

    public IReadOnlyList<string> Sizes => SizeNames;

    public IReadOnlyList<int> Zooms => ZoomSteps;

    public static string SizeName(ScriptSize size) => SizeNames[(int)size];
}
