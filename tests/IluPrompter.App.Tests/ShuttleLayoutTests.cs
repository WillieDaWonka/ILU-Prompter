using System.Windows;
using System.Windows.Shapes;
using IluPrompter.App.Logic;
using IluPrompter.App.Views.Drawings;
using IluPrompter.Core;

namespace IluPrompter.App.Tests;

/// <summary>
/// The redrawn ShuttlePRO v2 (button number to position) and the jog wheel's angle per click. Fakes only: no device.
/// </summary>
[Collection("WpfTheme")]
public class ShuttleLayoutTests
{
    private static readonly InputSourceId Contour = new(ControllerKind.JogShuttle, "\\\\?\\HID#fake-shuttle");

    private static void EnsureTheme()
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (app.Resources.MergedDictionaries.Count > 0) return;
        app.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/IluPrompter.Theme;component/Themes/IluControls.xaml", UriKind.Relative)));
    }

    [Fact]
    public void EveryShuttleProButton_FromOneToFifteen_HasExactlyOnePosition_AndEveryPositionOneButton()
    {
        var numbers = ShuttleProLayout.Buttons.ToList();
        Assert.Equal(Enumerable.Range(1, 15), numbers);
        var positions = numbers.Select(ShuttleProLayout.PositionOf).ToList();
        Assert.Equal(15, positions.Distinct().Count());
        Assert.Equal(Enum.GetValues<ShuttleProPosition>().OrderBy(p => p), positions.OrderBy(p => p));
    }

    [Theory]
    [InlineData(1, ShuttleProPosition.TopRow1)]
    [InlineData(4, ShuttleProPosition.TopRow4)]
    [InlineData(5, ShuttleProPosition.SecondRow1)]
    [InlineData(7, ShuttleProPosition.SecondRow3)]
    [InlineData(9, ShuttleProPosition.SecondRow5)]
    [InlineData(10, ShuttleProPosition.CurveUpperLeft)]
    [InlineData(11, ShuttleProPosition.CurveUpperRight)]
    [InlineData(12, ShuttleProPosition.CurveLowerLeft)]
    [InlineData(13, ShuttleProPosition.CurveLowerRight)]
    [InlineData(14, ShuttleProPosition.SideLeft)]
    [InlineData(15, ShuttleProPosition.SideRight)]
    public void TheNumbering_FollowsContoursManual_LeftToRightTopToBottom_SideButtonsLast(int button, ShuttleProPosition position) =>
        Assert.Equal(position, ShuttleProLayout.PositionOf(button));

    [WpfFact]
    public void TheDrawing_HasAPartForEveryPosition_AndLightsTheOneForTheButtonPressed()
    {
        EnsureTheme();
        var drawing = new ShuttleProDrawing();   // throws if a position has no part
        foreach (var position in Enum.GetValues<ShuttleProPosition>())
            Assert.NotNull(drawing.FindName(ShuttleProLayout.PartName(position)));

        var model = BuildModel(t => t.Record(new ButtonInput(Contour, ControlIds.ShuttleButton(12), 0, true), 10_000));
        drawing.Apply(model.Frame, false);
        var lit = (Shape)drawing.FindName(ShuttleProLayout.PartName(ShuttleProPosition.CurveLowerLeft))!;
        var other = (Shape)drawing.FindName(ShuttleProLayout.PartName(ShuttleProPosition.CurveLowerRight))!;
        Assert.NotEqual(lit.Fill, other.Fill);
    }

    // ===== jog steps to angle =====

    [Fact]
    public void TheJogWheel_TurnsThreeSixtyOverItsDetentsPerRevolution_OneNamedConstant()
    {
        Assert.Equal(10, ContourJog.DetentsPerRevolution);
        Assert.Equal(36, ContourJog.DegreesPerStep);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 36)]
    [InlineData(4, 144)]
    [InlineData(-1, -36)]
    [InlineData(-3, -108)]
    [InlineData(10, 0)]    // a whole turn looks the same
    [InlineData(13, 108)]
    [InlineData(-12, -72)]
    public void JogSteps_GiveTheAngle_ClockwiseForwardAndNegativeBack(int steps, double degrees) =>
        Assert.Equal(degrees, ContourJog.AngleDegrees(steps));


    [Fact]
    public void TheFrame_CarriesTheJogAngle_FromTheNetClicks()
    {
        var model = BuildModel(t => t.Record(new RelativeInput(Contour, ControlIds.JogWheel, 0, 3), 10_000));
        Assert.Equal(3, model.Frame.Shuttle!.JogSteps);
        Assert.Equal(108, model.Frame.Shuttle.JogAngle);

        var negative = BuildModel(t => t.Record(new RelativeInput(Contour, ControlIds.JogWheel, 0, -2), 10_000));
        Assert.Equal(-72, negative.Frame.Shuttle!.JogAngle);
        Assert.NotEqual(model.Frame.Signature, negative.Frame.Signature);
    }

    private static ControllerPanelModel BuildModel(Action<ControllerInputTable> feed)
    {
        var table = new ControllerInputTable();
        feed(table);
        var input = PrompterSettings.Default.Input with { SelectedController = ControllerKind.JogShuttle };
        var status = new DeviceStatus(Contour, "Contour ShuttlePRO v2", DeviceConnection.Connected, BatteryLevel.Unknown);
        return ControllerModelBuilder.Build(new ControllerBuildInput(input, ScrollMode.Display, status, false, table.Snapshot(), 10_050, false));
    }
}
