using IluPrompter.Core;
using Linearstar.Windows.RawInput.Native;

namespace IluPrompter.Input.Tests;

public class RawMouseNormaliserTests
{
    [Theory]
    [InlineData(120, 1.0)]
    [InlineData(-120, -1.0)]
    [InlineData(240, 2.0)]
    [InlineData(60, 0.5)]
    [InlineData(30, 0.25)]
    [InlineData(1, 1.0 / 120.0)]
    public void WheelNotches_keeps_fractions(int data, double expected) =>
        Assert.Equal(expected, RawMouseNormaliser.WheelNotches(data));

    [Fact]
    public void WheelNotches_reads_usButtonData_as_signed_16_bit()
    {
        // RAWMOUSE.usButtonData is unsigned on the wire: -120 arrives as 0xFF88 (65416).
        Assert.Equal(-1.0, RawMouseNormaliser.WheelNotches(0xFF88));
        Assert.Equal(-0.25, RawMouseNormaliser.WheelNotches(0xFFE2));   // -30
        Assert.Equal(1.0, RawMouseNormaliser.WheelNotches(0x0078));
    }

    [Fact]
    public void Vertical_wheel_flag_only()
    {
        Assert.True(RawMouseNormaliser.HasVerticalWheel(RawMouseButtonFlags.MouseWheel));
        Assert.True(RawMouseNormaliser.HasVerticalWheel(RawMouseButtonFlags.MouseWheel | RawMouseButtonFlags.MiddleButtonDown));
        Assert.False(RawMouseNormaliser.HasVerticalWheel(RawMouseButtonFlags.MouseHorizontalWheel));
        Assert.False(RawMouseNormaliser.HasVerticalWheel(RawMouseButtonFlags.None));
    }

    [Fact]
    public void Middle_and_X_buttons_become_edges()
    {
        var edges = new ButtonEdge[RawMouseNormaliser.MaxEdges];

        var n = RawMouseNormaliser.ButtonEdges(RawMouseButtonFlags.MiddleButtonDown, edges);
        Assert.Equal(1, n);
        Assert.Equal(new ButtonEdge(ControlIds.MiddleButton, true), edges[0]);

        n = RawMouseNormaliser.ButtonEdges(RawMouseButtonFlags.MiddleButtonUp, edges);
        Assert.Equal(1, n);
        Assert.Equal(new ButtonEdge(ControlIds.MiddleButton, false), edges[0]);

        n = RawMouseNormaliser.ButtonEdges(RawMouseButtonFlags.Button4Down | RawMouseButtonFlags.Button5Up, edges);
        Assert.Equal(2, n);
        Assert.Equal(new ButtonEdge(ControlIds.XButton1, true), edges[0]);
        Assert.Equal(new ButtonEdge(ControlIds.XButton2, false), edges[1]);
    }

    [Fact]
    public void Left_right_and_wheel_flags_produce_no_edges()
    {
        var edges = new ButtonEdge[RawMouseNormaliser.MaxEdges];
        var flags = RawMouseButtonFlags.LeftButtonDown | RawMouseButtonFlags.LeftButtonUp
            | RawMouseButtonFlags.RightButtonDown | RawMouseButtonFlags.RightButtonUp | RawMouseButtonFlags.MouseWheel;
        Assert.Equal(0, RawMouseNormaliser.ButtonEdges(flags, edges));
    }

    [Fact]
    public void All_six_edges_fit_in_the_buffer()
    {
        var edges = new ButtonEdge[RawMouseNormaliser.MaxEdges];
        var all = RawMouseButtonFlags.MiddleButtonDown | RawMouseButtonFlags.MiddleButtonUp
            | RawMouseButtonFlags.Button4Down | RawMouseButtonFlags.Button4Up
            | RawMouseButtonFlags.Button5Down | RawMouseButtonFlags.Button5Up;
        Assert.Equal(6, RawMouseNormaliser.ButtonEdges(all, edges));
    }

    [Fact]
    public void Device_paths_compare_case_insensitively_and_null_never_matches()
    {
        Assert.True(RawMouseNormaliser.SamePath(@"\\?\HID#VID_046D&PID_C52B#7&2a#{378de44c}", @"\\?\hid#vid_046d&pid_c52b#7&2a#{378DE44C}"));
        Assert.False(RawMouseNormaliser.SamePath(@"\\?\HID#A", @"\\?\HID#B"));
        Assert.False(RawMouseNormaliser.SamePath(null, null));
        Assert.False(RawMouseNormaliser.SamePath("", ""));
    }

    [Fact]
    public void Display_names()
    {
        Assert.Equal("MX Anywhere (VID_046D&PID_B01A)", RawMouseNormaliser.DisplayName(" MX Anywhere ", "Logitech", 0x046D, 0xB01A));
        Assert.Equal("Logitech mouse (VID_046D&PID_C52B)", RawMouseNormaliser.DisplayName(null, "Logitech", 0x046D, 0xC52B));
        Assert.Equal("Mouse (VID_0001&PID_000A)", RawMouseNormaliser.DisplayName("", "", 1, 10));
    }
}
