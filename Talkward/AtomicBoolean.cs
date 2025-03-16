using System.Threading;

namespace Talkward;

public struct AtomicBoolean
{
    private int _value;

    public AtomicBoolean(bool value)
    {
        _value = value ? -1 : 0;
    }

    public bool Value
    {
        get => Interlocked.CompareExchange(ref _value, 0, 0) == -1;
        set => Interlocked.Exchange(ref _value, value ? -1 : 0);
    }

    public void Set(bool value) => Value = value;

    public static implicit operator bool(AtomicBoolean atomicBoolean)
        => atomicBoolean.Value;

    public static explicit operator AtomicBoolean(bool value)
        => new(value);
}