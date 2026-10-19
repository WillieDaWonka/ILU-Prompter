namespace IluPrompter.Core;

/// <summary>Complete in the scaffold (frozen).</summary>
public sealed class KeyModeState : IKeyModeState
{
    private int _mode;

    public KeyMode Mode
    {
        get => (KeyMode)Volatile.Read(ref _mode);
        set
        {
            if (Interlocked.Exchange(ref _mode, (int)value) != (int)value) Changed?.Invoke(this, value);
        }
    }

    public event EventHandler<KeyMode>? Changed;
}
