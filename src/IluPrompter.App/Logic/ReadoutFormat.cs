using System.Globalization;
using IluPrompter.Core;

namespace IluPrompter.App.Logic;

/// <summary>
/// Text for the live column readouts (DESIGN.md "Speed control", "Readouts"). DM Mono figures, house duration format,
/// British spelling, a space before units. Pure.
/// </summary>
public static class ReadoutFormat
{
    public const string Minus = "−";
    public const string NoValue = "--";

    /// <summary>One decimal, with a sign only when negative ("4.5", "&#8722;2.0").</summary>
    public static string Speed(double speed)
    {
        var rounded = Math.Round(speed, 1, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0; // no "-0.0"
        var text = Math.Abs(rounded).ToString("0.0", CultureInfo.InvariantCulture);
        return rounded < 0 ? Minus + text : text;
    }

    /// <summary>"≈ 140 wpm" (rounded to 5), or "" when unknown.</summary>
    public static string Wpm(double wordsPerMinute)
    {
        if (double.IsNaN(wordsPerMinute) || double.IsInfinity(wordsPerMinute) || wordsPerMinute < 0.5) return "";
        var rounded = (int)(Math.Round(wordsPerMinute / 5.0, MidpointRounding.AwayFromZero) * 5);
        return $"≈ {rounded.ToString(CultureInfo.InvariantCulture)} wpm";
    }

    /// <summary>"0.8 s" for the Line scrolling auto-step delay.</summary>
    public static string StepDelay(double seconds) =>
        seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)
            ? NoValue
            : seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

    /// <summary>House duration format: "6m:42s" under an hour, "1h:05m" over, "--" when it can't be known.</summary>
    public static string Duration(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > TimeSpan.MaxValue.TotalSeconds / 2)
            return NoValue;
        return TimeEstimator.Format(TimeSpan.FromSeconds(Math.Round(seconds)));
    }

    /// <summary>"Line 112 / 384".</summary>
    public static string Line(EngineStatus status) =>
        status.LineCount <= 0 ? "Line 0 / 0" : $"Line {status.LineIndex + 1} / {status.LineCount}";

    /// <summary>"Section 3 / 7"; before the first marker the number is "-".</summary>
    public static string Section(EngineStatus status) =>
        status.SectionCount <= 0 ? "Section 0 / 0"
        : status.SectionIndex < 0 ? $"Section - / {status.SectionCount}"
        : $"Section {status.SectionIndex + 1} / {status.SectionCount}";

    /// <summary>Status bar position: "Line 112 of 384 · Section 3 of 7 Speaker 1".</summary>
    public static string Position(EngineStatus status, string? sectionName)
    {
        var line = status.LineCount <= 0 ? "Line 0 of 0" : $"Line {status.LineIndex + 1} of {status.LineCount}";
        if (status.SectionCount <= 0) return line;
        if (status.SectionIndex < 0) return $"{line} · Before the first section";
        var section = $"Section {status.SectionIndex + 1} of {status.SectionCount}";
        return string.IsNullOrWhiteSpace(sectionName) ? $"{line} · {section}" : $"{line} · {section} {sectionName}";
    }

    /// <summary>"1 912 words" (non-breaking space as the thousands separator, as in the wireframes).</summary>
    public static string Words(int count)
    {
        var digits = Math.Max(0, count).ToString(CultureInfo.InvariantCulture);
        var grouped = new System.Text.StringBuilder();
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) grouped.Append(' ');
            grouped.Append(digits[i]);
        }

        return grouped + (count == 1 ? " word" : " words");
    }

    /// <summary>"Saved 14:02" / "● Not saved" for the app bar.</summary>
    public static string SavedText(bool isDirty, DateTimeOffset? lastSaved) =>
        isDirty ? "● Not saved"
        : lastSaved is { } t ? "Saved " + t.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
        : "Not saved yet";
}
