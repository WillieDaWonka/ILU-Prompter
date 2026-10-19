using IluPrompter.App.Services;
using IluPrompter.Core;

namespace IluPrompter.App.ViewModels;

/// <summary>
/// All output layout in one place (DESIGN.md D7): lines visible, line spacing, the margin editor, and the
/// reading-line options. Shared by the live column (Wide) and Settings › Output.
/// </summary>
public sealed class OutputLayoutViewModel : SettingsBackedViewModel
{
    public const double MinLines = 4;
    public const double MaxLines = 16;
    public const double MinSpacing = 1.0;
    public const double MaxSpacing = 2.0;
    public const double MinReadingLinePercent = 10;
    public const double MaxReadingLinePercent = 60;

    public OutputLayoutViewModel(ISettingsService settings, IUiThread ui, MarginEditorViewModel margins)
        : base(settings, ui)
    {
        Margins = margins;
    }

    public MarginEditorViewModel Margins { get; }

    public double LinesVisible
    {
        get => Current.Text.LinesVisible;
        set
        {
            var v = Math.Round(Clamp(value, MinLines, MaxLines));
            if (Math.Abs(v - Current.Text.LinesVisible) < 0.0001) return;
            Update(s => s with { Text = s.Text with { LinesVisible = v } });
        }
    }

    public double LineSpacing
    {
        get => Current.Text.LineSpacing;
        set
        {
            var v = Math.Round(Clamp(value, MinSpacing, MaxSpacing) / 0.05) * 0.05;
            v = Math.Round(v, 2);
            if (Math.Abs(v - Current.Text.LineSpacing) < 0.0001) return;
            Update(s => s with { Text = s.Text with { LineSpacing = v } });
        }
    }

    /// <summary>Reading line position as % of the text area (10 to 60, default 33).</summary>
    public double ReadingLinePercent
    {
        get => Math.Round(Current.Output.ReadingLineFraction * 100.0);
        set
        {
            var v = Math.Round(Clamp(value, MinReadingLinePercent, MaxReadingLinePercent)) / 100.0;
            if (Math.Abs(v - Current.Output.ReadingLineFraction) < 0.0001) return;
            Update(s => s with { Output = s.Output with { ReadingLineFraction = v } });
        }
    }

    public bool ShowReadingMarker
    {
        get => Current.Output.ShowReadingMarker;
        set
        {
            if (value == Current.Output.ShowReadingMarker) return;
            Update(s => s with { Output = s.Output with { ShowReadingMarker = value } });
        }
    }

    public bool DimAboveReadingLine
    {
        get => Current.Output.DimAboveReadingLine;
        set
        {
            if (value == Current.Output.DimAboveReadingLine) return;
            Update(s => s with { Output = s.Output with { DimAboveReadingLine = value } });
        }
    }

    public bool ShowSectionNames
    {
        get => Current.Output.ShowSectionNames;
        set
        {
            if (value == Current.Output.ShowSectionNames) return;
            Update(s => s with { Output = s.Output with { ShowSectionNames = value } });
        }
    }

    /// <summary>Reset every output layout value to its factory default.</summary>
    public void ResetToDefaults()
    {
        var d = PrompterSettings.Default;
        Update(s => s with
        {
            Margins = d.Margins,
            Text = s.Text with { LinesVisible = d.Text.LinesVisible, LineSpacing = d.Text.LineSpacing },
            Output = s.Output with
            {
                ReadingLineFraction = d.Output.ReadingLineFraction,
                ShowReadingMarker = d.Output.ShowReadingMarker,
                DimAboveReadingLine = d.Output.DimAboveReadingLine,
                ShowSectionNames = d.Output.ShowSectionNames,
                Alignment = d.Output.Alignment,
            },
        });
        Margins.Refresh();
    }
}
