using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Talkward.Sam;

[PublicAPI, StructLayout(LayoutKind.Sequential, Size = 60)]
public struct Bytes60
{
    private byte _0;

    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => MemoryMarshal.CreateSpan(ref _0, 60);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<byte>(Bytes60 bytes60)
        => bytes60.Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(Bytes60 bytes60)
        => bytes60.Span;

    [SuppressMessage("ReSharper", "RedundantUnsafeContext",
        Justification = "Required for self-ref (_0)")]
    public unsafe ref byte this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) index >= 60)
                ThrowIndexOutOfRangeException();
            return ref Unsafe.Add(ref _0, index);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();
}