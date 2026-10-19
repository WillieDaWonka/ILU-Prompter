using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>What kind of event the last-input line came from.</summary>
public enum LastInputKind
{
    Button = 0,
    Axis,
    Relative,
}

/// <summary>
/// The most recent input, bound or not. <see cref="Value"/> is the axis value, or for a relative control the net amount
/// of its burst after this event (so "Wheel ▲ 3" keeps counting while the burst lasts).
/// </summary>
public sealed record LastInput(string ControlId, LastInputKind Kind, double Value);

/// <summary>One button, as the poll sees it.</summary>
public readonly record struct ButtonSnapshot(bool IsDown, long LastEdgeMs);

/// <summary>
/// One relative control (wheel, jog), as the poll sees it. <see cref="Total"/> is the net amount since the table was
/// cleared (for the wheel's treads and the jog's dimple); <see cref="Burst"/> the net amount of the current burst.
/// Positive is the event's own direction: away from you for the wheel, clockwise for the jog.
/// </summary>
public readonly record struct RelativeSnapshot(double Total, double Burst, long LastMs, long LastPositiveMs, long LastNegativeMs);

/// <summary>A copy of the table taken under its lock, for the UI thread to read at leisure.</summary>
public sealed record ControllerTableSnapshot(
    IReadOnlyDictionary<string, ButtonSnapshot> Buttons,
    IReadOnlyDictionary<string, double> Axes,
    IReadOnlyDictionary<string, RelativeSnapshot> Relatives,
    LastInput? Last,
    int MaxShuttleButton,
    bool HasInput)
{
    public static ControllerTableSnapshot Empty { get; } = new(
        new Dictionary<string, ButtonSnapshot>(), new Dictionary<string, double>(), new Dictionary<string, RelativeSnapshot>(), null, 0, false);
}

/// <summary>
/// The small per-control table the input thread writes and the 10 Hz poll reads (DESIGN.md "Updates: state changes only").
/// <see cref="Record"/> is called from provider threads for every <c>InputObserved</c> and does nothing but update a few
/// numbers under a lock: no WPF, no allocation per notch beyond the first sight of a control. A burst of 200 wheel
/// notches a second is 200 table writes, not 200 UI updates. Times are milliseconds from a monotonic clock supplied by
/// the caller, so tests need no real clock.
/// </summary>
public sealed class ControllerInputTable
{
    /// <summary>A button stays lit while down and this long after its last edge (WCAG 2.3.1: at most about three changes a second).</summary>
    public const long ButtonHoldMs = 300;

    /// <summary>The wheel and the jog have no release: lit this long after their last notch.</summary>
    public const long RelativeHoldMs = 400;

    /// <summary>A burst ends after this long without a notch; its count returns to 0.</summary>
    public const long BurstEndMs = 2000;

    /// <summary>An axis event only counts as "input" (for "No input yet" and the last-input line) above this size, so a
    /// resting stick's jitter does not read as use.</summary>
    public const double MinSignificantAxis = 0.05;

    private readonly object _gate = new();
    private readonly Dictionary<string, ButtonSnapshot> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _axes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RelativeSnapshot> _relatives = new(StringComparer.Ordinal);
    private LastInput? _last;
    private int _maxShuttleButton;
    private bool _hasInput;

    /// <summary>Record one event. <paramref name="axisThreshold"/> is how far an axis must move to count as input.</summary>
    public void Record(InputEvent input, long nowMs, double axisThreshold = MinSignificantAxis)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
        {
            switch (input)
            {
                case ButtonInput button:
                    _buttons[button.ControlId] = new ButtonSnapshot(button.IsPressed, nowMs);
                    if (button.IsPressed)
                    {
                        _hasInput = true;
                        _last = new LastInput(button.ControlId, LastInputKind.Button, 1);
                    }

                    if (TryShuttleButtonNumber(button.ControlId, out var number) && number > _maxShuttleButton) _maxShuttleButton = number;
                    break;

                case AxisInput axis:
                    var value = double.IsFinite(axis.Value) ? axis.Value : 0;
                    _axes[axis.ControlId] = value;
                    if (Math.Abs(value) > Math.Max(axisThreshold, 0.0))
                    {
                        _hasInput = true;
                        _last = new LastInput(axis.ControlId, LastInputKind.Axis, value);
                    }

                    break;

                case RelativeInput relative:
                    if (!double.IsFinite(relative.Amount) || relative.Amount == 0) break;
                    _relatives.TryGetValue(relative.ControlId, out var old);
                    var burst = old.LastMs > 0 && nowMs - old.LastMs < BurstEndMs ? old.Burst : 0;
                    burst += relative.Amount;
                    _relatives[relative.ControlId] = new RelativeSnapshot(
                        old.Total + relative.Amount,
                        burst,
                        nowMs,
                        relative.Amount > 0 ? nowMs : old.LastPositiveMs,
                        relative.Amount < 0 ? nowMs : old.LastNegativeMs);
                    _hasInput = true;
                    _last = new LastInput(relative.ControlId, LastInputKind.Relative, burst);
                    break;
            }
        }
    }

    /// <summary>Forget everything (the chosen controller changed, or the device was lost and came back).</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _buttons.Clear();
            _axes.Clear();
            _relatives.Clear();
            _last = null;
            _maxShuttleButton = 0;
            _hasInput = false;
        }
    }

    public ControllerTableSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ControllerTableSnapshot(
                new Dictionary<string, ButtonSnapshot>(_buttons, StringComparer.Ordinal),
                new Dictionary<string, double>(_axes, StringComparer.Ordinal),
                new Dictionary<string, RelativeSnapshot>(_relatives, StringComparer.Ordinal),
                _last,
                _maxShuttleButton,
                _hasInput);
        }
    }

    /// <summary>"Button7" gives 7. Only Contour button ids count.</summary>
    public static bool TryShuttleButtonNumber(string controlId, out int number)
    {
        number = 0;
        return controlId.StartsWith("Button", StringComparison.Ordinal)
               && int.TryParse(controlId.AsSpan("Button".Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out number)
               && number > 0;
    }

    /// <summary>A button is lit while it is down and for <see cref="ButtonHoldMs"/> after its last edge.</summary>
    public static bool IsLit(ButtonSnapshot button, long nowMs) => button.IsDown || nowMs - button.LastEdgeMs < ButtonHoldMs;

    /// <summary>The current burst net amount: 0 once <see cref="BurstEndMs"/> have passed without a notch.</summary>
    public static double CurrentBurst(RelativeSnapshot relative, long nowMs) =>
        relative.LastMs > 0 && nowMs - relative.LastMs < BurstEndMs ? relative.Burst : 0;

    /// <summary>True for <see cref="RelativeHoldMs"/> after the last event in the given time.</summary>
    public static bool WithinRelativeHold(long lastMs, long nowMs) => lastMs > 0 && nowMs - lastMs < RelativeHoldMs;
}
