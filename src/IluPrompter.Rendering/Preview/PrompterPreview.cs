using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IluPrompter.Core;
using IluPrompter.Documents;

namespace IluPrompter.Rendering;

/// <summary>Everything the preview needs; registered by AddIluRendering, resolved by the shell.</summary>
public sealed record PreviewServices(
    IScrollEngine Engine,
    IPromptDocumentService Documents,
    ISettingsService Settings,
    IOutputSurface Output,
    IOutputDocumentFactory DocumentFactory,
    ILayoutGeometryBuilder GeometryBuilder,
    IAppLog Log);

/// <summary>
/// The live preview (DESIGN.md D2): the output laid out at the output's logical size, scaled, letterboxed
/// in `backdrop`, with preview-only overlays (margin tint, reading line, BLANK dimming) and margin /
/// reading-line drag handles. The shell places it in XAML and calls Attach once.
/// Public surface frozen; stream C owns the implementation.
/// STUB: black box that ticks the engine when the output is not rendering.
/// </summary>
public sealed class PrompterPreview : Control
{
    private PreviewServices? _services;

    public PrompterPreview()
    {
        Background = Brushes.Black;
        Focusable = true;
    }

    /// <summary>True while the pointer is over the scaled script (the shell shows "Wheel drives the prompt here").</summary>
    public static readonly DependencyProperty IsPointerOverScriptProperty = DependencyProperty.Register(
        nameof(IsPointerOverScript), typeof(bool), typeof(PrompterPreview), new PropertyMetadata(false));

    public bool IsPointerOverScript
    {
        get => (bool)GetValue(IsPointerOverScriptProperty);
        private set => SetValue(IsPointerOverScriptProperty, value);
    }

    public void Attach(PreviewServices services)
    {
        _services = services;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Detach()
    {
        CompositionTarget.Rendering -= OnRendering;
        _services = null;
    }

    protected override void OnRender(DrawingContext drawingContext) =>
        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

    private void OnRendering(object? sender, EventArgs e) => _services?.Engine.TickIfStale(Stopwatch.GetTimestamp());
}
