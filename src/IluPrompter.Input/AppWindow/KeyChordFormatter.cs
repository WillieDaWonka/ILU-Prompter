using System.Windows.Input;
using IluPrompter.Core;

namespace IluPrompter.Input;

/// <summary>
/// Pure helpers that turn WPF key information into the canonical chord strings of
/// <see cref="ControlIds.KeyChord"/>. No UI objects are touched, so everything here is unit-testable.
/// </summary>
internal static class KeyChordFormatter
{
    /// <summary>
    /// Picks the key that actually went down. <c>Key.System</c> (Alt held, or F10) is replaced by
    /// <c>SystemKey</c>; <c>Key.ImeProcessed</c> and <c>Key.DeadCharProcessed</c> are text-composition
    /// events and give <see cref="Key.None"/> (ignored).
    /// </summary>
    public static Key Resolve(Key key, Key systemKey) => key switch
    {
        Key.System => systemKey,
        Key.ImeProcessed => Key.None,
        Key.DeadCharProcessed => Key.None,
        _ => key,
    };

    /// <summary>True for keys that only ever act as a modifier (or can never be a binding).</summary>
    public static bool IsModifierOrIgnored(Key key) => key switch
    {
        Key.None => true,
        Key.LeftCtrl or Key.RightCtrl => true,
        Key.LeftAlt or Key.RightAlt => true,
        Key.LeftShift or Key.RightShift => true,
        Key.LWin or Key.RWin => true,
        Key.System or Key.ImeProcessed or Key.DeadCharProcessed => true,
        _ => false,
    };

    /// <summary>
    /// Builds the chord for an already resolved key (see <see cref="Resolve"/>). Returns false for pure
    /// modifier presses and for anything pressed while the Windows key is held (the Windows key is never
    /// part of a chord).
    /// </summary>
    public static bool TryFormat(Key resolvedKey, ModifierKeys modifiers, out string chord)
    {
        chord = string.Empty;
        if (IsModifierOrIgnored(resolvedKey) || modifiers.HasFlag(ModifierKeys.Windows))
        {
            return false;
        }

        chord = ControlIds.KeyChord(
            modifiers.HasFlag(ModifierKeys.Control),
            modifiers.HasFlag(ModifierKeys.Alt),
            modifiers.HasFlag(ModifierKeys.Shift),
            CanonicalName(resolvedKey));
        return true;
    }

    /// <summary>
    /// One-call form of Resolve + TryFormat, taking the two keys WPF's <c>KeyEventArgs</c> exposes.
    /// </summary>
    public static bool TryFormat(Key key, Key systemKey, ModifierKeys modifiers, out string chord) =>
        TryFormat(Resolve(key, systemKey), modifiers, out chord);

    /// <summary>
    /// The <see cref="Key"/> enum name, with the aliased members pinned to one spelling. Several members
    /// share a value (Return/Enter, Prior/PageUp, Next/PageDown, Capital/CapsLock) and
    /// <c>Enum.ToString()</c> does not promise which name wins, so the names are fixed here.
    /// </summary>
    public static string CanonicalName(Key key) => key switch
    {
        Key.Return => "Enter",
        Key.Prior => "PageUp",
        Key.Next => "PageDown",
        Key.Capital => "CapsLock",
        _ => key.ToString(),
    };
}
