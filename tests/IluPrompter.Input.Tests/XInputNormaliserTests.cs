using IluPrompter.Core;
using Vortice.XInput;
using CoreBatteryLevel = IluPrompter.Core.BatteryLevel;
using XBatteryLevel = Vortice.XInput.BatteryLevel;

namespace IluPrompter.Input.Tests;

public class XInputNormaliserTests
{
    [Theory]
    [InlineData(short.MinValue, -1.0)]       // -32768 clamps to -1
    [InlineData(-32767, -1.0)]
    [InlineData(short.MaxValue, 1.0)]        // 32767 -> 1
    [InlineData(0, 0.0)]
    public void Thumb_normalises_to_unit_range(short raw, double expected) =>
        Assert.Equal(expected, XInputNormaliser.Thumb(raw));

    [Fact]
    public void Thumb_midpoints_are_proportional()
    {
        Assert.Equal(16384 / 32767.0, XInputNormaliser.Thumb(16384), 12);
        Assert.Equal(-16384 / 32767.0, XInputNormaliser.Thumb(-16384), 12);
    }

    [Theory]
    [InlineData((byte)0, 0.0)]
    [InlineData((byte)255, 1.0)]
    [InlineData((byte)51, 0.2)]
    public void Trigger_is_raw_over_255(byte raw, double expected) =>
        Assert.Equal(expected, XInputNormaliser.Trigger(raw), 12);

    [Fact]
    public void Axis_change_threshold_is_strictly_greater_than_0_002()
    {
        Assert.False(XInputNormaliser.AxisChanged(0.5, 0.5));
        Assert.False(XInputNormaliser.AxisChanged(0.5, 0.5015));
        Assert.False(XInputNormaliser.AxisChanged(0.0, 0.002));     // exactly the threshold: not greater
        Assert.True(XInputNormaliser.AxisChanged(0.5, 0.5021));
        Assert.True(XInputNormaliser.AxisChanged(0.5, 0.4979));
    }

    [Fact]
    public void Returning_to_exactly_zero_always_fires()
    {
        Assert.True(XInputNormaliser.AxisChanged(0.0015, 0.0));
        Assert.True(XInputNormaliser.AxisChanged(-0.001, 0.0));
        Assert.False(XInputNormaliser.AxisChanged(0.0, 0.0));
    }

    [Fact]
    public void One_trigger_step_is_above_the_threshold()
    {
        // 1/255 = 0.0039 > 0.002, so every trigger step raises an event.
        Assert.True(XInputNormaliser.AxisChanged(XInputNormaliser.Trigger(10), XInputNormaliser.Trigger(11)));
    }

    [Fact]
    public void ReadAxes_follows_AxisIds_order()
    {
        var pad = new Gamepad
        {
            LeftThumbX = 32767,
            LeftThumbY = -32768,
            RightThumbX = 0,
            RightThumbY = 16384,
            LeftTrigger = 255,
            RightTrigger = 51,
        };
        var v = new double[6];
        XInputNormaliser.ReadAxes(pad, v);

        Assert.Equal(new[]
        {
            ControlIds.LeftThumbX, ControlIds.LeftThumbY, ControlIds.RightThumbX,
            ControlIds.RightThumbY, ControlIds.LeftTrigger, ControlIds.RightTrigger,
        }, XInputNormaliser.AxisIds);
        Assert.Equal(1.0, v[0]);
        Assert.Equal(-1.0, v[1]);
        Assert.Equal(0.0, v[2]);
        Assert.Equal(16384 / 32767.0, v[3], 12);
        Assert.Equal(1.0, v[4]);
        Assert.Equal(0.2, v[5], 12);
    }

    [Theory]
    [InlineData(GamepadButtons.A, ControlIds.A)]
    [InlineData(GamepadButtons.B, ControlIds.B)]
    [InlineData(GamepadButtons.X, ControlIds.X)]
    [InlineData(GamepadButtons.Y, ControlIds.Y)]
    [InlineData(GamepadButtons.DPadUp, ControlIds.DPadUp)]
    [InlineData(GamepadButtons.DPadDown, ControlIds.DPadDown)]
    [InlineData(GamepadButtons.DPadLeft, ControlIds.DPadLeft)]
    [InlineData(GamepadButtons.DPadRight, ControlIds.DPadRight)]
    [InlineData(GamepadButtons.LeftShoulder, ControlIds.LeftShoulder)]
    [InlineData(GamepadButtons.RightShoulder, ControlIds.RightShoulder)]
    [InlineData(GamepadButtons.Back, "Back")]       // View button
    [InlineData(GamepadButtons.Start, "Start")]     // Menu button
    [InlineData(GamepadButtons.LeftThumb, "LeftThumb")]
    [InlineData(GamepadButtons.RightThumb, "RightThumb")]
    public void Each_button_flag_maps_to_its_canonical_id(GamepadButtons flag, string id)
    {
        var edges = new ButtonEdge[XInputNormaliser.MaxEdges];
        var n = XInputNormaliser.DiffButtons(GamepadButtons.None, flag, edges);
        Assert.Equal(1, n);
        Assert.Equal(new ButtonEdge(id, true), edges[0]);

        n = XInputNormaliser.DiffButtons(flag, GamepadButtons.None, edges);
        Assert.Equal(1, n);
        Assert.Equal(new ButtonEdge(id, false), edges[0]);
    }

    [Fact]
    public void View_and_Menu_match_ControlIds()
    {
        Assert.Equal(ControlIds.View, Id(GamepadButtons.Back));
        Assert.Equal(ControlIds.Menu, Id(GamepadButtons.Start));
        Assert.Equal(ControlIds.LeftThumbPress, Id(GamepadButtons.LeftThumb));
        Assert.Equal(ControlIds.RightThumbPress, Id(GamepadButtons.RightThumb));

        static string Id(GamepadButtons f) => XInputNormaliser.ButtonTable.Single(t => t.Flag == f).Id;
    }

    [Fact]
    public void Held_buttons_do_not_repeat_and_only_changes_produce_edges()
    {
        var edges = new ButtonEdge[XInputNormaliser.MaxEdges];
        var held = GamepadButtons.A | GamepadButtons.DPadUp;

        Assert.Equal(0, XInputNormaliser.DiffButtons(held, held, edges));

        // A released, B pressed, DPadUp still held.
        var n = XInputNormaliser.DiffButtons(held, GamepadButtons.DPadUp | GamepadButtons.B, edges);
        Assert.Equal(2, n);
        var got = edges.Take(n).ToHashSet();
        Assert.Contains(new ButtonEdge(ControlIds.A, false), got);
        Assert.Contains(new ButtonEdge(ControlIds.B, true), got);
    }

    [Fact]
    public void Guide_is_not_reported()
    {
        var edges = new ButtonEdge[XInputNormaliser.MaxEdges];
        Assert.Equal(0, XInputNormaliser.DiffButtons(GamepadButtons.None, GamepadButtons.Guide, edges));
    }

    [Fact]
    public void Edge_buffer_is_large_enough_for_every_button_at_once()
    {
        var edges = new ButtonEdge[XInputNormaliser.MaxEdges];
        var all = XInputNormaliser.ButtonTable.Aggregate(GamepadButtons.None, (acc, t) => acc | t.Flag);
        Assert.Equal(XInputNormaliser.ButtonTable.Length, XInputNormaliser.DiffButtons(GamepadButtons.None, all, edges));
        Assert.True(XInputNormaliser.MaxEdges >= XInputNormaliser.ButtonTable.Length);
    }

    [Theory]
    [InlineData(null, -1)]
    [InlineData("", -1)]
    [InlineData("xinput:0", 0)]
    [InlineData("xinput:3", 3)]
    [InlineData("XINPUT:2", 2)]
    [InlineData("xinput:4", -1)]
    [InlineData("xinput:-1", -1)]
    [InlineData("xinput:x", -1)]
    [InlineData("hid:0", -1)]
    public void DeviceId_parsing(string? id, int expected) =>
        Assert.Equal(expected, XInputNormaliser.ParseDeviceId(id));

    [Fact]
    public void DeviceId_round_trips_and_names_are_one_based()
    {
        for (var i = 0; i < XInputNormaliser.SlotCount; i++)
        {
            Assert.Equal(i, XInputNormaliser.ParseDeviceId(XInputNormaliser.DeviceId(i)));
        }

        Assert.Equal("xinput:2", XInputNormaliser.DeviceId(2));
        Assert.Equal("Xbox controller 1", XInputNormaliser.DisplayName(0));
    }

    [Theory]
    [InlineData(BatteryType.Wired, XBatteryLevel.Empty, CoreBatteryLevel.Wired)]
    [InlineData(BatteryType.Disconnected, XBatteryLevel.Full, CoreBatteryLevel.Unknown)]
    [InlineData(BatteryType.Unknown, XBatteryLevel.Full, CoreBatteryLevel.Unknown)]
    [InlineData(BatteryType.Alkaline, XBatteryLevel.Empty, CoreBatteryLevel.Empty)]
    [InlineData(BatteryType.Nimh, XBatteryLevel.Low, CoreBatteryLevel.Low)]
    [InlineData(BatteryType.Alkaline, XBatteryLevel.Medium, CoreBatteryLevel.Medium)]
    [InlineData(BatteryType.Nimh, XBatteryLevel.Full, CoreBatteryLevel.Full)]
    public void Battery_mapping(BatteryType type, XBatteryLevel level, CoreBatteryLevel expected) =>
        Assert.Equal(expected, XInputNormaliser.Battery(type, level));
}
