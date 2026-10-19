namespace IluPrompter.Input;

/// <summary>
/// One decoded Contour report. <see cref="Ring"/> is the spring-loaded shuttle ring position (-7..+7),
/// <see cref="Jog"/> is the free jog wheel's wrapping 8-bit counter, <see cref="Buttons"/> is the 16-bit
/// button mask (bit 0 = first raw button).
/// </summary>
internal readonly record struct ShuttleReport(sbyte Ring, byte Jog, ushort Buttons);

/// <summary>Pure decoding of the Contour ShuttleXpress / ShuttlePRO HID input report. No device access.</summary>
internal static class ContourShuttleReportParser
{
    /// <summary>The device report: ring, jog, unused, buttons lo, buttons hi.</summary>
    public const int ReportLength = 5;

    /// <summary>
    /// Accepts the raw 5-byte report, or 6 or more bytes whose first byte is the report id 0x00 (what
    /// Windows <c>ReadFile</c> / HidSharp return). Anything else is rejected.
    /// Layout after the optional id: byte 0 = ring (int8), byte 1 = jog counter, byte 2 unused,
    /// bytes 3-4 = button mask, little-endian.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> report, out ShuttleReport result)
    {
        result = default;
        ReadOnlySpan<byte> body;
        if (report.Length == ReportLength)
        {
            body = report;
        }
        else if (report.Length > ReportLength && report[0] == 0x00)
        {
            body = report.Slice(1, ReportLength);
        }
        else
        {
            return false;
        }

        result = new ShuttleReport(
            unchecked((sbyte)body[0]),
            body[1],
            (ushort)(body[3] | (body[4] << 8)));
        return true;
    }

    /// <summary>Signed jog movement between two counter readings, wrapping at 256: 255 -> 0 is +1, 0 -> 255 is -1.</summary>
    public static sbyte JogDelta(byte previous, byte current) => unchecked((sbyte)(current - previous));

    /// <summary>Ring position -7..+7 as -1..+1 (k / 7). Out-of-range raw values are clamped.</summary>
    public static double RingValue(sbyte ring) => Math.Clamp((int)ring, -7, 7) / 7.0;
}
