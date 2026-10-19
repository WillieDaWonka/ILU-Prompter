using System.Globalization;
using System.Text;
using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>Everything the builder needs from one poll.</summary>
public sealed record ControllerBuildInput(
    InputSettings Input,
    ScrollMode Mode,
    DeviceStatus? Status,
    bool OutputIsLive,
    ControllerTableSnapshot Table,
    long NowMs,
    bool Small);

/// <summary>
/// Turns the input table, the current bindings and the device status into what the Controller tab shows: the lit state
/// of every part, the "what it did" rows, the device line and the last-input line (DESIGN.md "Controller drawing").
/// Pure: no WPF, no clock, no threads. The bound-action label of each input comes from the same bindings the router uses.
/// </summary>
public static class ControllerModelBuilder
{
    public const double XboxLeftStickDeadzone = 0.24;
    public const double XboxRightStickDeadzone = 0.265;
    public const double XboxTriggerDeadzone = 0.12;

    private const double StickRingRadius = 18;
    private const double StickDotTravel = 13;
    private const double TriggerHeight = 60;

    private static readonly string[] XboxButtonOrder =
    [
        ControlIds.A, ControlIds.B, ControlIds.X, ControlIds.Y,
        ControlIds.DPadUp, ControlIds.DPadDown, ControlIds.DPadLeft, ControlIds.DPadRight,
        ControlIds.LeftShoulder, ControlIds.RightShoulder, ControlIds.View, ControlIds.Menu,
    ];

    private static readonly string[] MouseButtonOrder = [ControlIds.MiddleButton, ControlIds.XButton1, ControlIds.XButton2];

    public static ControllerPanelModel Build(ControllerBuildInput i)
    {
        ArgumentNullException.ThrowIfNull(i);
        var kind = i.Input.SelectedController;
        if (kind == ControllerKind.Keyboard)
        {
            return Finish(new ControllerPanelModel
            {
                State = ControllerPanelState.NotChosen,
                MessageTitle = "No controller chosen.",
                MessageDetail = "The keyboard drives the prompt.",
                BarText = "No controller",
            });
        }

        var drawing = DrawingFor(kind, i.Status, i.Table);
        var connection = kind == ControllerKind.MouseWheel ? DeviceConnection.Connected : i.Status?.Connection ?? DeviceConnection.NotFound;
        var last = LastText(i);
        var (device, detail) = DeviceLines(i, kind, drawing);

        if (connection != DeviceConnection.Connected)
        {
            var lost = connection == DeviceConnection.Disconnected;
            var name = ChipMapper.FullName(kind);
            return Finish(new ControllerPanelModel
            {
                State = lost ? ControllerPanelState.Lost : ControllerPanelState.NotFound,
                Frame = Sign(new ControllerFrame { Kind = drawing, Missing = true }),
                DeviceLine = device,
                DetailLine = detail,
                Chip = ChipMapper.Controller(kind, i.Status, i.OutputIsLive),
                MessageTitle = lost ? $"The {name} stopped answering. Reconnect it." : $"No {name} found. Connect it, then wait a moment.",
                MessageDetail = "The keyboard still works: Space plays and pauses.",
                LastText = last,
                BarText = ChipMapper.ControllerName(kind) + " missing",
            });
        }

        var (frame, persistent, order) = drawing switch
        {
            ControllerDrawingKind.Mouse => MouseFrame(i),
            ControllerDrawingKind.Xbox => XboxFrame(i),
            _ => ShuttleFrame(i, drawing),
        };

        var hasInput = i.Table.HasInput;
        var rows = new List<ControllerRow>();
        if (hasInput)
        {
            var maxRows = i.Small ? 3 : 4;
            var transientCap = Math.Min(i.Small ? 1 : 2, Math.Max(0, maxRows - persistent.Count));
            rows.AddRange(persistent.Take(maxRows));
            foreach (var id in order)
            {
                if (transientCap == 0) break;
                if (frame.Look(id) is not (PartLook.Lit or PartLook.Flash)) continue;
                rows.Add(new ControllerRow(ControllerLabels.ShortName(id), ButtonAction(i, kind, id), "", true));
                transientCap--;
            }
        }

        var (battery, batteryText) = kind == ControllerKind.Xbox ? BatteryOf(i.Status) : (BatteryGlyph.None, "");
        return Finish(new ControllerPanelModel
        {
            State = hasInput ? ControllerPanelState.InUse : ControllerPanelState.NoInputYet,
            Frame = frame,
            DeviceLine = device,
            DetailLine = detail,
            Battery = battery,
            BatteryText = batteryText,
            Rows = rows,
            Hint = hasInput ? "" : HintFor(kind, i.Input),
            LastText = last,
            BarText = hasInput ? last : "No input yet",
        });
    }

    /// <summary>
    /// Only the tab bar's last-input line: what the poll computes while the drawing is not on screen (panel hidden, the
    /// other tab open, the flyout closed). Same words as <see cref="Build"/>'s <c>BarText</c>.
    /// </summary>
    public static string BarText(ControllerBuildInput i)
    {
        ArgumentNullException.ThrowIfNull(i);
        var kind = i.Input.SelectedController;
        if (kind == ControllerKind.Keyboard) return "No controller";
        var connection = kind == ControllerKind.MouseWheel ? DeviceConnection.Connected : i.Status?.Connection ?? DeviceConnection.NotFound;
        if (connection != DeviceConnection.Connected) return ChipMapper.ControllerName(kind) + " missing";
        return i.Table.HasInput ? LastText(i) : "No input yet";
    }

    /// <summary>
    /// The ShuttlePRO v2 drawing (the venue's unit) is the default for a jog-shuttle: it is drawn when the device's name says
    /// PRO, when no name is known yet, and whenever a button above 5 has been seen. Only a device that names itself a
    /// ShuttleXpress and has shown no button above 5 gets the ShuttleXpress drawing.
    /// </summary>
    public static ControllerDrawingKind DrawingFor(ControllerKind kind, DeviceStatus? status, ControllerTableSnapshot table) => kind switch
    {
        ControllerKind.MouseWheel => ControllerDrawingKind.Mouse,
        ControllerKind.Xbox => ControllerDrawingKind.Xbox,
        ControllerKind.JogShuttle =>
            (status?.DisplayName?.Contains("Xpress", StringComparison.OrdinalIgnoreCase) ?? false) && table.MaxShuttleButton <= 5
                ? ControllerDrawingKind.ShuttleXpress
                : ControllerDrawingKind.ShuttlePro,
        _ => ControllerDrawingKind.None,
    };

    // ===== lit state =====

    private static PartLook LookOf(bool bound, ControllerTableSnapshot table, string id, long now)
    {
        var lit = table.Buttons.TryGetValue(id, out var entry) && ControllerInputTable.IsLit(entry, now);
        return bound ? lit ? PartLook.Lit : PartLook.Bound : lit ? PartLook.Flash : PartLook.Unbound;
    }

    private static Dictionary<string, PartLook> ButtonLooks(ControllerBuildInput i, IEnumerable<string> ids, IReadOnlyDictionary<string, BindingAction> map)
    {
        var looks = new Dictionary<string, PartLook>(StringComparer.Ordinal);
        foreach (var id in ids) looks[id] = LookOf(ControllerLabels.IsBound(map, id, i.Mode), i.Table, id, i.NowMs);
        return looks;
    }

    private static AxisBinding? BoundAxis(IReadOnlyDictionary<string, AxisBinding> axes, string id) =>
        axes.TryGetValue(id, out var b) && b.Role != AxisRole.None ? b : null;

    // ===== mouse =====

    private static (ControllerFrame, List<ControllerRow>, string[]) MouseFrame(ControllerBuildInput i)
    {
        var wheel = i.Input.Wheel;
        var looks = ButtonLooks(i, MouseButtonOrder, wheel.Buttons);
        i.Table.Relatives.TryGetValue(ControlIds.Wheel, out var rel);
        var role = wheel.Wheel.For(i.Mode);
        var bound = role != RelativeRole.None;
        var up = ControllerInputTable.WithinRelativeHold(rel.LastPositiveMs, i.NowMs);
        var down = ControllerInputTable.WithinRelativeHold(rel.LastNegativeMs, i.NowMs);
        var phase = Mod((int)Math.Round(-rel.Total, MidpointRounding.AwayFromZero), 4);
        var frame = Sign(new ControllerFrame
        {
            Kind = ControllerDrawingKind.Mouse,
            Buttons = looks,
            Wheel = new WheelFrame(bound, phase, up, down),
        });

        var rows = new List<ControllerRow>();
        if (bound)
        {
            var lit = ControllerInputTable.WithinRelativeHold(rel.LastMs, i.NowMs);
            var burst = ControllerInputTable.CurrentBurst(rel, i.NowMs);
            var action = lit && burst != 0
                ? ControllerLabels.RelativeLit(wheel.Wheel, role, ControllerLabels.ForwardUnits(ControllerKind.MouseWheel, wheel.Wheel, burst))
                : ControllerLabels.RelativeRest(role);
            rows.Add(new ControllerRow(ControllerLabels.ShortName(ControlIds.Wheel), action, ControllerLabels.WheelBurst(burst), lit));
        }

        return (frame, rows, MouseButtonOrder);
    }

    // ===== xbox =====

    private static (ControllerFrame, List<ControllerRow>, string[]) XboxFrame(ControllerBuildInput i)
    {
        var xbox = i.Input.Xbox;
        var order = XboxButtonOrder.Concat([ControlIds.LeftThumbPress, ControlIds.RightThumbPress]).ToArray();
        var looks = ButtonLooks(i, order, xbox.Buttons);
        var rows = new List<ControllerRow>();

        var triggers = new List<TriggerFrame>();
        foreach (var id in new[] { ControlIds.RightTrigger, ControlIds.LeftTrigger })
        {
            var binding = BoundAxis(xbox.Axes, id);
            var pull = Math.Clamp(i.Table.Axes.TryGetValue(id, out var v) ? v : 0, 0, 1);
            var dz = binding?.Deadzone ?? XboxTriggerDeadzone;
            var fill = (int)Math.Round(pull * TriggerHeight, MidpointRounding.AwayFromZero);
            var indicator = binding is null ? IndicatorLook.Unbound : pull > dz ? IndicatorLook.Bound : IndicatorLook.Idle;
            triggers.Add(new TriggerFrame(id, binding is not null, fill, Math.Round(TriggerHeight * (1 - dz), 1), indicator));
            if (binding is not null)
                rows.Add(new ControllerRow(ControllerLabels.ShortName(id), ControllerLabels.AxisAction(binding.Role), ControllerLabels.Percent(pull), pull > dz));
        }

        var sticks = new List<StickFrame>();
        foreach (var (press, idX, idY, defaultDz) in new[]
                 {
                     (ControlIds.LeftThumbPress, ControlIds.LeftThumbX, ControlIds.LeftThumbY, XboxLeftStickDeadzone),
                     (ControlIds.RightThumbPress, ControlIds.RightThumbX, ControlIds.RightThumbY, XboxRightStickDeadzone),
                 })
        {
            var xb = BoundAxis(xbox.Axes, idX);
            var yb = BoundAxis(xbox.Axes, idY);
            var x = Math.Clamp(i.Table.Axes.TryGetValue(idX, out var xv) ? xv : 0, -1, 1);
            var y = Math.Clamp(i.Table.Axes.TryGetValue(idY, out var yv) ? yv : 0, -1, 1);
            var length = Math.Sqrt((x * x) + (y * y));
            if (length > 1)
            {
                x /= length;
                y /= length;
                length = 1;
            }

            var dz = yb?.Deadzone ?? xb?.Deadzone ?? defaultDz;
            var axisBound = xb is not null || yb is not null;
            var pressBound = ControllerLabels.IsBound(xbox.Buttons, press, i.Mode);
            var lit = looks[press] is PartLook.Lit or PartLook.Flash;
            sticks.Add(new StickFrame(
                press,
                axisBound || pressBound,
                lit ? pressBound ? PressLook.Lit : PressLook.Flash : PressLook.None,
                (int)Math.Round(StickDotTravel * x, MidpointRounding.AwayFromZero),
                (int)Math.Round(-StickDotTravel * y, MidpointRounding.AwayFromZero),
                Math.Round(Math.Max(3, StickRingRadius * dz), 1),
                !axisBound ? IndicatorLook.Unbound : length > dz ? IndicatorLook.Bound : IndicatorLook.Idle));

            foreach (var (id, binding) in new[] { (idX, xb), (idY, yb) })
            {
                if (binding is null) continue;
                var name = ControllerLabels.ShortName(id) + (xb is not null && yb is not null ? (id == idX ? " X" : " Y") : "");
                var raw = i.Table.Axes.TryGetValue(id, out var rv) ? rv : 0;
                rows.Add(new ControllerRow(name, ControllerLabels.AxisAction(binding.Role), ControllerLabels.SignedPercent(raw), Math.Abs(raw) > binding.Deadzone));
            }
        }

        var frame = Sign(new ControllerFrame { Kind = ControllerDrawingKind.Xbox, Buttons = looks, Sticks = sticks, Triggers = triggers });
        return (frame, rows, order);
    }

    // ===== contour =====

    private static (ControllerFrame, List<ControllerRow>, string[]) ShuttleFrame(ControllerBuildInput i, ControllerDrawingKind drawing)
    {
        var shuttle = i.Input.Shuttle;
        var count = drawing == ControllerDrawingKind.ShuttlePro ? 15 : 5;
        var ids = Enumerable.Range(1, count).Select(ControlIds.ShuttleButton).ToArray();
        var looks = ButtonLooks(i, ids, shuttle.Buttons);

        var ringBinding = BoundAxis(shuttle.Axes, ControlIds.ShuttleRing);
        var ringValue = Math.Clamp(i.Table.Axes.TryGetValue(ControlIds.ShuttleRing, out var rv) ? rv : 0, -1, 1);
        var k = Math.Clamp((int)Math.Round(ringValue * 7, MidpointRounding.AwayFromZero), -7, 7);
        var arc = ringBinding is null ? IndicatorLook.Unbound : k != 0 ? IndicatorLook.Bound : IndicatorLook.Idle;

        i.Table.Relatives.TryGetValue(ControlIds.JogWheel, out var rel);
        var jogRole = shuttle.Jog.For(i.Mode);
        var jogLit = ControllerInputTable.WithinRelativeHold(rel.LastMs, i.NowMs);
        var dimple = Mod((int)Math.Round(rel.Total, MidpointRounding.AwayFromZero), 10);

        var frame = Sign(new ControllerFrame
        {
            Kind = drawing,
            Buttons = looks,
            Shuttle = new ShuttleFrame(ringBinding is not null, k, arc, jogRole != RelativeRole.None, dimple, jogLit),
        });

        var rows = new List<ControllerRow>();
        if (ringBinding is not null)
            rows.Add(new ControllerRow(ControllerLabels.ShortName(ControlIds.ShuttleRing), ControllerLabels.AxisAction(ringBinding.Role), ControllerLabels.RingPosition(k), k != 0));
        if (jogRole != RelativeRole.None)
        {
            var burst = ControllerInputTable.CurrentBurst(rel, i.NowMs);
            var action = jogLit && burst != 0
                ? ControllerLabels.RelativeLit(shuttle.Jog, jogRole, ControllerLabels.ForwardUnits(ControllerKind.JogShuttle, shuttle.Jog, burst))
                : ControllerLabels.RelativeRest(jogRole);
            rows.Add(new ControllerRow(ControllerLabels.ShortName(ControlIds.JogWheel), action, ControllerLabels.JogBurst(burst), jogLit));
        }

        return (frame, rows, ids);
    }

    // ===== words =====

    private static string ButtonAction(ControllerBuildInput i, ControllerKind kind, string id)
    {
        var map = ButtonMap(i.Input, kind);
        // A stick press is keyed by its ControlId in the Xbox button map as well.
        return map is null ? ControllerLabels.NotUsed : ControllerLabels.Button(map, id, i.Mode);
    }

    private static IReadOnlyDictionary<string, BindingAction>? ButtonMap(InputSettings input, ControllerKind kind) => kind switch
    {
        ControllerKind.MouseWheel => input.Wheel.Buttons,
        ControllerKind.Xbox => input.Xbox.Buttons,
        ControllerKind.JogShuttle => input.Shuttle.Buttons,
        _ => null,
    };

    private static string LastText(ControllerBuildInput i)
    {
        if (i.Table.Last is not { } last) return "";
        var kind = i.Input.SelectedController;
        var name = ControllerLabels.ShortName(last.ControlId);
        switch (last.Kind)
        {
            case LastInputKind.Button:
                return $"Last: {name} · {ButtonAction(i, kind, last.ControlId)}";

            case LastInputKind.Axis:
            {
                var axes = kind == ControllerKind.Xbox ? i.Input.Xbox.Axes : kind == ControllerKind.JogShuttle ? i.Input.Shuttle.Axes : null;
                var binding = axes is not null ? BoundAxis(axes, last.ControlId) : null;
                var value = last.ControlId switch
                {
                    ControlIds.LeftTrigger or ControlIds.RightTrigger => ControllerLabels.Percent(last.Value),
                    ControlIds.ShuttleRing => ControllerLabels.RingPosition(Math.Clamp((int)Math.Round(last.Value * 7, MidpointRounding.AwayFromZero), -7, 7)),
                    _ => ControllerLabels.SignedPercent(last.Value),
                };
                return $"Last: {name} {value} · {(binding is null ? ControllerLabels.NotUsed : ControllerLabels.AxisAction(binding.Role))}";
            }

            default:
            {
                var isWheel = last.ControlId == ControlIds.Wheel;
                var binding = isWheel ? i.Input.Wheel.Wheel : i.Input.Shuttle.Jog;
                var role = binding.For(i.Mode);
                var value = isWheel ? ControllerLabels.WheelBurst(last.Value) : ControllerLabels.JogBurst(last.Value);
                var action = role == RelativeRole.None
                    ? ControllerLabels.NotUsed
                    : ControllerLabels.RelativeLit(binding, role, ControllerLabels.ForwardUnits(kind, binding, last.Value));
                return $"Last: {name} {value} · {action}";
            }
        }
    }

    private static (string Device, string Detail) DeviceLines(ControllerBuildInput i, ControllerKind kind, ControllerDrawingKind drawing)
    {
        switch (kind)
        {
            case ControllerKind.MouseWheel:
                var dedicated = i.Input.Wheel.DedicatedDeviceId is not null;
                return dedicated
                    ? ("Mouse wheel · second mouse", string.IsNullOrWhiteSpace(i.Status?.DisplayName) ? "Second mouse" : i.Status!.DisplayName)
                    : ("Mouse wheel · your mouse", "Over the preview only");
            case ControllerKind.Xbox:
                var index = PlayerIndex(i.Status, i.Input.Xbox.UserIndex);
                return (index is { } n ? $"Xbox controller · player {n.ToString(CultureInfo.InvariantCulture)}" : "Xbox controller", "");
            default:
                return ("Jog-shuttle", drawing == ControllerDrawingKind.ShuttleXpress ? "Contour ShuttleXpress" : "Contour ShuttlePRO v2");
        }
    }

    private static int? PlayerIndex(DeviceStatus? status, int configured)
    {
        var id = status?.Source.DeviceId;
        if (id is not null && id.StartsWith("xinput:", StringComparison.Ordinal)
            && int.TryParse(id.AsSpan("xinput:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n))
            return n + 1;
        return configured >= 0 ? configured + 1 : null;
    }

    private static (BatteryGlyph, string) BatteryOf(DeviceStatus? status) => status?.Battery switch
    {
        BatteryLevel.Full => (BatteryGlyph.Full, "Battery full"),
        BatteryLevel.Medium => (BatteryGlyph.Medium, "Battery medium"),
        BatteryLevel.Low => (BatteryGlyph.Low, "Battery low"),
        BatteryLevel.Empty => (BatteryGlyph.Empty, "Battery empty"),
        BatteryLevel.Wired => (BatteryGlyph.Wired, "Wired"),
        _ => (BatteryGlyph.None, ""),
    };

    private static string HintFor(ControllerKind kind, InputSettings input) => kind switch
    {
        ControllerKind.MouseWheel => input.Wheel.DedicatedDeviceId is null ? "Turn the wheel over the preview." : "Turn the wheel.",
        ControllerKind.Xbox => "Press a button or move a stick.",
        _ => "Turn the ring or the jog wheel.",
    };

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    // ===== signatures =====

    private static ControllerFrame Sign(ControllerFrame frame)
    {
        var sb = new StringBuilder();
        sb.Append(frame.Kind).Append('|').Append(frame.Missing ? 'M' : '-');
        foreach (var (id, look) in frame.Buttons.OrderBy(b => b.Key, StringComparer.Ordinal)) sb.Append('|').Append(id).Append('=').Append((int)look);
        foreach (var s in frame.Sticks) sb.Append("|S").Append(s.Id).Append(s.Bound ? 'b' : 'u').Append((int)s.Press).Append(',').Append(s.DotDx).Append(',').Append(s.DotDy).Append(',').Append(s.DeadzoneRadius.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append((int)s.Dot);
        foreach (var t in frame.Triggers) sb.Append("|T").Append(t.Id).Append(t.Bound ? 'b' : 'u').Append(',').Append(t.FillPx).Append(',').Append(t.TickOffsetFromTop.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append((int)t.Fill);
        if (frame.Wheel is { } w) sb.Append("|W").Append(w.Bound ? 'b' : 'u').Append(',').Append(w.TreadPhase).Append(w.UpLit ? 'U' : '-').Append(w.DownLit ? 'D' : '-');
        if (frame.Shuttle is { } h) sb.Append("|H").Append(h.RingBound ? 'b' : 'u').Append(',').Append(h.RingK).Append(',').Append((int)h.RingArc).Append(',').Append(h.JogBound ? 'b' : 'u').Append(',').Append(h.JogDimple).Append(h.JogLit ? 'L' : '-');
        return frame with { Signature = sb.ToString() };
    }

    private static ControllerPanelModel Finish(ControllerPanelModel model)
    {
        var sb = new StringBuilder();
        sb.Append((int)model.State).Append('|').Append(model.Frame.Signature).Append('|').Append(model.DeviceLine).Append('|').Append(model.DetailLine)
            .Append('|').Append((int)model.Battery).Append(model.BatteryText).Append('|').Append(model.Chip?.Kind).Append(model.Chip?.Automation)
            .Append('|').Append(model.MessageTitle).Append('|').Append(model.MessageDetail).Append('|').Append(model.Hint)
            .Append('|').Append(model.LastText).Append('|').Append(model.BarText);
        foreach (var r in model.Rows) sb.Append("|R").Append(r.Control).Append('/').Append(r.Action).Append('/').Append(r.Value).Append(r.Lit ? '!' : '.');
        return model with { Signature = sb.ToString() };
    }
}
