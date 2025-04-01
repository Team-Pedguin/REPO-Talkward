using UnityEngine;

namespace Talkward;

public sealed class UnityTimeProvider : ITimeProvider
{
    public double UnscaledTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Time.unscaledTimeAsDouble;
    }
}