using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>
/// The Contour jog wheel: how far the drawing turns for each click. One named constant, so a measured value on the
/// venue's unit is a one-line fix.
/// </summary>
public static class ContourJog
{
    /// <summary>
    /// Clicks (detents) in one full turn of the jog wheel: 10, from the LinuxCNC <c>shuttle</c> driver manual, which describes
    /// the ShuttleXpress, ShuttlePRO and ShuttlePRO v2 as having "a 10 counts/revolution jog wheel with detents" (one source;
    /// verify on the device). Contour's own manual gives no figure.
    /// </summary>
    public const int DetentsPerRevolution = 10;

    /// <summary>Degrees the jog wheel (and the picture on it) turns for one click.</summary>
    public const double DegreesPerStep = 360.0 / DetentsPerRevolution;

    /// <summary>
    /// The angle for a net count of clicks, clockwise positive (a click forward turns the wheel clockwise). Whole turns are
    /// dropped (the result is within a turn and keeps the sign), which looks the same and keeps the number small.
    /// </summary>
    public static double AngleDegrees(int steps) => (steps % DetentsPerRevolution) * DegreesPerStep;
}

/// <summary>Where a ShuttlePRO v2 button sits on the unit. Each member names one drawn part in <c>ShuttleProDrawing.xaml</c> (<c>Pos_&lt;name&gt;</c>).</summary>
public enum ShuttleProPosition
{
    /// <summary>The top row of four oval buttons, left to right.</summary>
    TopRow1,
    TopRow2,
    TopRow3,
    TopRow4,

    /// <summary>The second row of five, left to right: the outer two sit lower, at the shoulders, angled; the inner three just below the top row.</summary>
    SecondRow1,
    SecondRow2,
    SecondRow3,
    SecondRow4,
    SecondRow5,

    /// <summary>The four long curved buttons below the ring: the upper pair (nearer the ring), then the lower pair (above the palm rest).</summary>
    CurveUpperLeft,
    CurveUpperRight,
    CurveLowerLeft,
    CurveLowerRight,

    /// <summary>The tall side button on each side of the ring.</summary>
    SideLeft,
    SideRight,
}

/// <summary>
/// The ONE table that puts a ShuttlePRO v2's logical button numbers (the provider's 1 to 15: raw bit + 1) on the picture.
/// <para>
/// Source: Contour's ShuttlePRO v2 manual (as reproduced at twistidea.com/manual/contour-shuttle-pro-2-manual): buttons are
/// numbered left to right, top row to bottom row, the two side buttons last: 1 to 4 the top row, 5 to 9 the second row, 10 and
/// 11 the third row (two buttons below the ring), 12 and 13 the fourth row (above the palm rest), 14 the left side button,
/// 15 the right. Consistent with the LinuxCNC <c>shuttle</c> driver (the older 13-button ShuttlePRO has bits 0 to 12, so 14 and
/// 15 are the v2's additions, the side buttons) and the Linux Cinelerra manual (K14 and K15 work only on the v2). One
/// community project (shuttle-go) names the buttons differently (F1 to F9, M1 and M2, B1 to B4) and gives no bit numbers.
/// VERIFY ON THE DEVICE: press each button with the Controller tab open and check that the right part lights. A wrong
/// position is a one-line fix below.
/// </para>
/// </summary>
public static class ShuttleProLayout
{
    private static readonly Dictionary<int, ShuttleProPosition> Table = new()
    {
        [1] = ShuttleProPosition.TopRow1,
        [2] = ShuttleProPosition.TopRow2,
        [3] = ShuttleProPosition.TopRow3,
        [4] = ShuttleProPosition.TopRow4,
        [5] = ShuttleProPosition.SecondRow1,
        [6] = ShuttleProPosition.SecondRow2,
        [7] = ShuttleProPosition.SecondRow3,
        [8] = ShuttleProPosition.SecondRow4,
        [9] = ShuttleProPosition.SecondRow5,
        [10] = ShuttleProPosition.CurveUpperLeft,
        [11] = ShuttleProPosition.CurveUpperRight,
        [12] = ShuttleProPosition.CurveLowerLeft,
        [13] = ShuttleProPosition.CurveLowerRight,
        [14] = ShuttleProPosition.SideLeft,
        [15] = ShuttleProPosition.SideRight,
    };

    public const int ButtonCount = 15;

    /// <summary>The logical button numbers, 1 to 15.</summary>
    public static IEnumerable<int> Buttons => Enumerable.Range(1, ButtonCount);

    public static ShuttleProPosition PositionOf(int button) => Table[button];

    /// <summary>The name of the drawn part for a position.</summary>
    public static string PartName(ShuttleProPosition position) => "Pos_" + position;
}
