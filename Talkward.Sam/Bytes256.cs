using UnityEngine;

namespace Talkward.Sam;

[PublicAPI, StructLayout(LayoutKind.Sequential, Size = 256)]
public struct Bytes256
{
    private byte _0;

    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _0, 256);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<byte>(Bytes256 bytes256)
        => bytes256.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(Bytes256 bytes256)
        => bytes256.Span;

    [SuppressMessage("ReSharper", "RedundantUnsafeContext",
        Justification = "Required for self-ref (_0)")]
    public unsafe ref byte this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) index >= 256)
                ThrowIndexOutOfRangeException();
            return ref Unsafe.Add(ref _0, index);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();
}