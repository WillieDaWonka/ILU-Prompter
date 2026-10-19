using IluPrompter.Core;

namespace IluPrompter.Input.Tests;

public class ContourShuttleReportParserTests
{
    [Fact]
    public void Parses_a_5_byte_report()
    {
        // ring = +3, jog = 200, unused, buttons = 0x0110 (little-endian 10 01)
        byte[] raw = { 0x03, 200, 0x00, 0x10, 0x01 };
        Assert.True(ContourShuttleReportParser.TryParse(raw, out var r));
        Assert.Equal((sbyte)3, r.Ring);
        Assert.Equal((byte)200, r.Jog);
        Assert.Equal((ushort)0x0110, r.Buttons);
    }

    [Fact]
    public void Parses_a_6_byte_report_with_leading_report_id()
    {
        byte[] raw = { 0x00, 0xFD, 7, 0x00, 0x20, 0x80 };      // ring = -3
        Assert.True(ContourShuttleReportParser.TryParse(raw, out var r));
        Assert.Equal((sbyte)-3, r.Ring);
        Assert.Equal((byte)7, r.Jog);
        Assert.Equal((ushort)0x8020, r.Buttons);
    }

    [Fact]
    public void Longer_reports_with_id_use_the_first_five_payload_bytes()
    {
        byte[] raw = { 0x00, 0x07, 1, 0, 0x01, 0x00, 0xAA, 0xBB };
        Assert.True(ContourShuttleReportParser.TryParse(raw, out var r));
        Assert.Equal((sbyte)7, r.Ring);
        Assert.Equal((ushort)1, r.Buttons);
    }

    [Fact]
    public void Id_prefixed_and_bare_forms_of_the_same_state_agree()
    {
        byte[] bare = { 0xF9, 42, 0, 0xFF, 0x7F };
        byte[] withId = { 0x00, 0xF9, 42, 0, 0xFF, 0x7F };
        Assert.True(ContourShuttleReportParser.TryParse(bare, out var a));
        Assert.True(ContourShuttleReportParser.TryParse(withId, out var b));
        Assert.Equal(a, b);
        Assert.Equal((sbyte)-7, a.Ring);
        Assert.Equal((ushort)0x7FFF, a.Buttons);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void Too_short_reports_are_rejected(int length) =>
        Assert.False(ContourShuttleReportParser.TryParse(new byte[length], out _));

    [Fact]
    public void Six_bytes_without_a_zero_report_id_are_rejected()
    {
        byte[] raw = { 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
        Assert.False(ContourShuttleReportParser.TryParse(raw, out _));
    }

    [Theory]
    [InlineData(-7, -1.0)]
    [InlineData(-1, -1.0 / 7.0)]
    [InlineData(0, 0.0)]
    [InlineData(1, 1.0 / 7.0)]
    [InlineData(7, 1.0)]
    public void Ring_is_k_over_7(int k, double expected) =>
        Assert.Equal(expected, ContourShuttleReportParser.RingValue((sbyte)k), 12);

    [Fact]
    public void Ring_covers_all_fifteen_positions_monotonically()
    {
        var last = double.NegativeInfinity;
        for (var k = -7; k <= 7; k++)
        {
            var v = ContourShuttleReportParser.RingValue((sbyte)k);
            Assert.True(v > last);
            Assert.InRange(v, -1.0, 1.0);
            last = v;
        }
    }

    [Fact]
    public void Out_of_range_ring_bytes_are_clamped()
    {
        Assert.Equal(1.0, ContourShuttleReportParser.RingValue(100));
        Assert.Equal(-1.0, ContourShuttleReportParser.RingValue(-128));
    }

    [Theory]
    [InlineData(255, 0, 1)]     // wrap forward
    [InlineData(0, 255, -1)]    // wrap back
    [InlineData(10, 11, 1)]
    [InlineData(11, 10, -1)]
    [InlineData(5, 5, 0)]
    [InlineData(250, 4, 10)]
    [InlineData(4, 250, -10)]
    [InlineData(0, 127, 127)]
    [InlineData(0, 128, -128)]
    public void Jog_delta_wraps(int previous, int current, int expected) =>
        Assert.Equal(expected, ContourShuttleReportParser.JogDelta((byte)previous, (byte)current));
}

public class ContourModelsTests
{
    [Fact]
    public void Known_vid_pids_are_recognised()
    {
        Assert.True(ContourModels.TryGet(0x0B33, 0x0020, out var xpress));
        Assert.Equal("ShuttleXpress", xpress.Name);
        Assert.True(ContourModels.TryGet(0x0B33, 0x0030, out var pro2));
        Assert.Equal(15, pro2.ButtonCount);
        Assert.True(ContourModels.TryGet(0x05F3, 0x0240, out _));
    }

    [Theory]
    [InlineData(0x0B33, 0x0021)]
    [InlineData(0x046D, 0x0020)]
    [InlineData(0x05F3, 0x0020)]
    public void Other_devices_are_not_Contour(int vid, int pid) =>
        Assert.False(ContourModels.TryGet(vid, pid, out _));

    [Fact]
    public void ShuttleXpress_maps_bits_4_to_8_to_buttons_1_to_5()
    {
        var m = ContourModels.ShuttleXpress;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, m.LogicalButton(4 + i));
            Assert.Equal("Button" + (i + 1), m.ControlId(4 + i));
        }
    }

    [Fact]
    public void ShuttlePro_maps_bits_0_to_14_to_buttons_1_to_15()
    {
        var m = ContourModels.ShuttleProV2;
        for (var bit = 0; bit < 15; bit++)
        {
            Assert.Equal(bit + 1, m.LogicalButton(bit));
        }
    }

    [Fact]
    public void Unknown_bits_pass_through_as_bit_plus_one()
    {
        Assert.Equal(1, ContourModels.ShuttleXpress.LogicalButton(0));
        Assert.Equal(16, ContourModels.ShuttleXpress.LogicalButton(15));
        Assert.Equal(16, ContourModels.ShuttleProV2.LogicalButton(15));
        Assert.Equal(100, ContourModels.ShuttleProV2.LogicalButton(99));
    }
}

public class ShuttleReportTrackerTests
{
    private static ShuttleReport R(int ring, int jog, int buttons) => new((sbyte)ring, (byte)jog, (ushort)buttons);

    private static ShuttleReportTracker NewXpress() => new(ContourModels.ShuttleXpress);

    [Fact]
    public void First_report_is_a_baseline_with_ring_but_no_jog_or_buttons()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];

        var c = t.Update(R(ring: 3, jog: 200, buttons: 0x0030), edges);

        Assert.True(c.IsBaseline);
        Assert.True(c.RingChanged);                 // ring is reported once on connect
        Assert.Equal(3 / 7.0, c.Ring, 12);
        Assert.Equal(0, c.JogDelta);                // jog baseline: no movement event
        Assert.Equal(0, c.ButtonEdgeCount);         // buttons already held do not fire
        Assert.True(t.HasBaseline);
    }

    [Fact]
    public void Jog_after_baseline_is_a_wrapped_difference()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 255, 0), edges);

        Assert.Equal(1, t.Update(R(0, 0, 0), edges).JogDelta);       // 255 -> 0
        Assert.Equal(-1, t.Update(R(0, 255, 0), edges).JogDelta);    // 0 -> 255
        Assert.Equal(0, t.Update(R(0, 255, 0), edges).JogDelta);
        Assert.Equal(3, t.Update(R(0, 2, 0), edges).JogDelta);       // 255 -> 2
    }

    [Fact]
    public void Ring_event_only_when_the_position_changes()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 0, 0), edges);

        Assert.False(t.Update(R(0, 1, 0), edges).RingChanged);       // jog only
        var c = t.Update(R(-2, 1, 0), edges);
        Assert.True(c.RingChanged);
        Assert.Equal(-2 / 7.0, c.Ring, 12);
        Assert.False(t.Update(R(-2, 1, 0), edges).RingChanged);
        Assert.True(t.Update(R(0, 1, 0), edges).RingChanged);        // spring return to centre is reported
    }

    [Fact]
    public void Button_edges_are_diffed_and_mapped_to_logical_numbers()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 0, 0), edges);

        // raw bit 6 = logical button 3 (the centre one in the default mapping)
        var press = t.Update(R(0, 0, 1 << 6), edges);
        Assert.Equal(1, press.ButtonEdgeCount);
        Assert.Equal(new ButtonEdge(ControlIds.ShuttleButton(3), true), edges[0]);

        // held: nothing
        Assert.Equal(0, t.Update(R(0, 0, 1 << 6), edges).ButtonEdgeCount);

        // swap: bit 6 up, bit 8 (button 5) down
        var swap = t.Update(R(0, 0, 1 << 8), edges);
        Assert.Equal(2, swap.ButtonEdgeCount);
        var got = edges.Take(2).ToHashSet();
        Assert.Contains(new ButtonEdge(ControlIds.ShuttleButton(3), false), got);
        Assert.Contains(new ButtonEdge(ControlIds.ShuttleButton(5), true), got);
    }

    [Fact]
    public void ShuttlePro_buttons_use_identity_mapping()
    {
        var t = new ShuttleReportTracker(ContourModels.ShuttleProV2);
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 0, 0), edges);

        var c = t.Update(R(0, 0, (1 << 0) | (1 << 14)), edges);
        Assert.Equal(2, c.ButtonEdgeCount);
        Assert.Equal(new ButtonEdge("Button1", true), edges[0]);
        Assert.Equal(new ButtonEdge("Button15", true), edges[1]);
    }

    [Fact]
    public void All_sixteen_bits_fit_in_the_edge_buffer()
    {
        var t = new ShuttleReportTracker(ContourModels.ShuttleProV2);
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 0, 0), edges);
        Assert.Equal(16, t.Update(R(0, 0, 0xFFFF), edges).ButtonEdgeCount);
    }

    [Fact]
    public void Reset_makes_the_next_report_a_new_baseline()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];
        t.Update(R(0, 10, 0), edges);
        t.Update(R(0, 20, 0), edges);

        t.Reset();
        Assert.False(t.HasBaseline);

        var c = t.Update(R(0, 99, 1 << 4), edges);
        Assert.True(c.IsBaseline);
        Assert.Equal(0, c.JogDelta);
        Assert.Equal(0, c.ButtonEdgeCount);
    }

    [Fact]
    public void Parsed_bytes_flow_through_the_tracker()
    {
        var t = NewXpress();
        var edges = new ButtonEdge[ShuttleReportTracker.MaxEdges];

        byte[] first = { 0x00, 0x00, 250, 0x00, 0x00, 0x00 };
        byte[] second = { 0x00, 0x02, 3, 0x00, 0x10, 0x00 };    // ring +2, jog 250 -> 3 (+9), bit 4 down
        Assert.True(ContourShuttleReportParser.TryParse(first, out var r1));
        Assert.True(ContourShuttleReportParser.TryParse(second, out var r2));

        t.Update(r1, edges);
        var c = t.Update(r2, edges);

        Assert.True(c.RingChanged);
        Assert.Equal(2 / 7.0, c.Ring, 12);
        Assert.Equal(9, c.JogDelta);
        Assert.Equal(1, c.ButtonEdgeCount);
        Assert.Equal(new ButtonEdge("Button1", true), edges[0]);
    }
}
