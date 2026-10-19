namespace IluPrompter.Core;

/// <summary>
/// Bounded, lock-free, allocation-free multi-producer queue of <see cref="EngineCommand"/> structs
/// (Vyukov's bounded MPMC ring with per-cell sequence numbers). The engine drains it from a single consumer
/// at a time (under its own gate), so the dequeue side needs no atomics beyond the cell sequence.
/// ConcurrentQueue is not used because it allocates a new segment whenever the current one fills.
/// </summary>
internal sealed class EngineCommandQueue
{
    private readonly Cell[] _cells;
    private readonly long _mask;
    private long _enqueuePos;
    private long _dequeuePos;
    private long _dropped;

    public EngineCommandQueue(int capacityPowerOfTwo)
    {
        if (capacityPowerOfTwo < 2 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityPowerOfTwo), "Capacity must be a power of two >= 2.");
        }

        _cells = new Cell[capacityPowerOfTwo];
        _mask = capacityPowerOfTwo - 1;
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i].Sequence = i;
        }
    }

    /// <summary>Commands that were dropped because the ring stayed full (the ticker is not running).</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>Any thread. Spins briefly if the ring is full, then drops the command and counts it.</summary>
    public void Enqueue(in EngineCommand command)
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            if (TryEnqueue(command))
            {
                return;
            }

            if (attempt < 8) { Thread.SpinWait(20); } else { Thread.Yield(); }
        }

        Interlocked.Increment(ref _dropped);
    }

    /// <summary>Single consumer at a time (the caller holds the engine gate).</summary>
    public bool TryDequeue(out EngineCommand command)
    {
        var pos = _dequeuePos;
        ref var cell = ref _cells[pos & _mask];
        var seq = Volatile.Read(ref cell.Sequence);
        if (seq - (pos + 1) == 0)
        {
            _dequeuePos = pos + 1;
            command = cell.Command;
            Volatile.Write(ref cell.Sequence, pos + _mask + 1);
            return true;
        }

        command = default;
        return false;
    }

    private bool TryEnqueue(in EngineCommand command)
    {
        var pos = Volatile.Read(ref _enqueuePos);
        for (;;)
        {
            ref var cell = ref _cells[pos & _mask];
            var seq = Volatile.Read(ref cell.Sequence);
            var diff = seq - pos;
            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref _enqueuePos, pos + 1, pos) == pos)
                {
                    cell.Command = command;
                    Volatile.Write(ref cell.Sequence, pos + 1);
                    return true;
                }

                pos = Volatile.Read(ref _enqueuePos);
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                pos = Volatile.Read(ref _enqueuePos);
            }
        }
    }

    private struct Cell
    {
        public long Sequence;
        public EngineCommand Command;
    }
}
