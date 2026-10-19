using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IluPrompter.App.Logic;
using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>One row of the sections list.</summary>
public sealed partial class SectionRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _durationText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTag))]
    private bool _isCurrent;

    public SectionRowViewModel(int index, string name)
    {
        Index = index;
        _name = name;
    }

    public int Index { get; }

    /// <summary>"1" ... shown before the name.</summary>
    public string Number => (Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"current" while this is the section at the reading line (the theme's Tag value for the row).</summary>
    public string CurrentTag => IsCurrent ? "current" : "";

    public bool HasDuration => DurationText.Length > 0;

    partial void OnDurationTextChanged(string value) => OnPropertyChanged(nameof(HasDuration));
}

/// <summary>What the sections list asks of the editor.</summary>
public interface ISectionEditing
{
    /// <summary>Scroll the editor to the section marker and put the caret there. The prompt does not move.</summary>
    void ShowSection(int sectionIndex);

    void RenameSection(int sectionIndex, string newName);

    void DeleteSectionMarker(int sectionIndex);
}

/// <summary>
/// The sections panel (DESIGN.md B). Rows come from the published snapshot; durations from
/// <see cref="TimeEstimator.SectionDurations"/> at the set speed; the current row is the engine's section at the
/// reading line. Single click shows the section in the editor; double-click or Enter jumps the prompt there.
/// </summary>
public sealed partial class SectionsViewModel : ObservableObject
{
    private readonly IPromptDocumentService _documents;
    private readonly IScrollEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IUiThread _ui;
    private ISectionEditing? _editing;
    private IReadOnlyList<SectionInfo> _lastSections = Array.Empty<SectionInfo>();
    private LayoutGeometry? _lastGeometry;
    private double _lastSpeed = double.NaN;
    private int _lastCurrent = int.MinValue;
    private bool _refreshing;

    [ObservableProperty]
    private SectionRowViewModel? _selected;

    [ObservableProperty]
    private bool _isFlyoutOpen;

    public SectionsViewModel(IPromptDocumentService documents, IScrollEngine engine, ISettingsService settings, IUiThread ui)
    {
        _documents = documents;
        _engine = engine;
        _settings = settings;
        _ui = ui;
        documents.SnapshotPublished += (_, _) => _ui.Post(Refresh);
        Refresh();
    }

    public ObservableCollection<SectionRowViewModel> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    public string EmptyText => "No sections yet. Press Ctrl+Shift+Enter in the script to add one.";

    public void Attach(ISectionEditing editing) => _editing = editing;

    /// <summary>Rebuild rows if the section list changed, then refresh durations and the current row.</summary>
    public void Refresh()
    {
        var sections = _documents.Current.Sections;
        if (!SameSections(sections, _lastSections))
        {
            _lastSections = sections;
            var selectedIndex = Selected?.Index ?? -1;
            // Restoring the selection raises the list's SelectionChanged, which would "show the section in the editor"
            // and pull the caret away from where you are typing (integration fix): ignore it while rebuilding.
            _refreshing = true;
            try
            {
                Rows.Clear();
                foreach (var s in sections) Rows.Add(new SectionRowViewModel(s.Index, s.Name));
                Selected = selectedIndex >= 0 && selectedIndex < Rows.Count ? Rows[selectedIndex] : null;
            }
            finally
            {
                _refreshing = false;
            }
            _lastGeometry = null;
            _lastCurrent = int.MinValue;
            OnPropertyChanged(nameof(IsEmpty));
        }

        Poll();
    }

    /// <summary>Called at 10 Hz with the engine status already read: cheap when nothing changed.</summary>
    public void Poll()
    {
        var status = _engine.Status;
        var geometry = _engine.Geometry;
        var speed = status.SetSpeed;
        if (!ReferenceEquals(geometry, _lastGeometry) || Math.Abs(speed - _lastSpeed) > 0.0001 || double.IsNaN(_lastSpeed))
        {
            _lastGeometry = geometry;
            _lastSpeed = speed;
            UpdateDurations(geometry, speed);
        }

        if (status.SectionIndex != _lastCurrent)
        {
            _lastCurrent = status.SectionIndex;
            foreach (var row in Rows) row.IsCurrent = row.Index == status.SectionIndex;
        }
    }

    public void ShowInEditor(SectionRowViewModel? row)
    {
        if (row is null || _refreshing) return;
        _editing?.ShowSection(row.Index);
    }

    /// <summary>Double-click or Enter: a cut, then paused. Closes the flyout.</summary>
    public void JumpTo(SectionRowViewModel? row)
    {
        if (row is null) return;
        _engine.Post(EngineCommand.GoToSection(row.Index));
        IsFlyoutOpen = false;
    }

    public void Rename(SectionRowViewModel? row, string newName)
    {
        if (row is null || string.IsNullOrWhiteSpace(newName)) return;
        _editing?.RenameSection(row.Index, newName.Trim());
    }

    public void DeleteMarker(SectionRowViewModel? row)
    {
        if (row is null) return;
        _editing?.DeleteSectionMarker(row.Index);
    }

    private void UpdateDurations(LayoutGeometry? geometry, double speed)
    {
        if (geometry is null || Rows.Count == 0) return;
        var durations = TimeEstimator.SectionDurations(geometry, speed, _settings.Current.Scroll.Curve);
        for (var i = 0; i < Rows.Count; i++)
        {
            var seconds = i < durations.Count ? durations[i].TotalSeconds : 0;
            Rows[i].DurationText = seconds >= 1 ? ReadoutFormat.Duration(seconds) : "";
        }
    }

    private static bool SameSections(IReadOnlyList<SectionInfo> a, IReadOnlyList<SectionInfo> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].Name != b[i].Name || a[i].ParagraphIndex != b[i].ParagraphIndex) return false;
        return true;
    }
}
