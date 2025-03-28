using System.Buffers;

namespace Talkward.Sam;

/// <summary>
/// Represents a mutable copy of a raw resource.
/// </summary>
[PublicAPI, StructLayout(LayoutKind.Sequential)]
public class LazyRawResourceMutableCopy
{
    /// <summary>
    /// The raw resource being copied.
    /// </summary>
    internal readonly RawResource _resource;
    
    public unsafe ref readonly RawResource Original
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _resource!;
    }

    /// <summary>
    /// The mutable copy of the raw resource.
    /// </summary>
    internal IMemoryOwner<byte>? _copy;
    
    internal unsafe IMemoryOwner<byte> Copy
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _copy ??= Original.GetMutableCopy();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LazyRawResourceMutableCopy"/> struct.
    /// </summary>
    /// <param name="resource">The raw resource to copy.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LazyRawResourceMutableCopy(RawResource resource)
    {
        _resource = resource;
        _copy = null;
    }

    /// <summary>
    /// Gets the span of the mutable copy of the raw resource.
    /// </summary>
    public Span<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Copy.Memory.Span;
    }

    /// <summary>
    /// Gets a reference to the byte at the specified index in the mutable copy of the raw resource.
    /// </summary>
    /// <param name="index"></param>
    public ref byte this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Span[index];
    }

    public static implicit operator LazyRawResourceMutableCopy(RawResource raw)
        => new(raw);

    public static implicit operator Span<byte>(LazyRawResourceMutableCopy lazy)
        => lazy.Span;
}

/*
internal static class LazyRawResourceMutableCopyHelper
{
    
    /// <summary>
    /// Gets or creates a mutable copy of the raw resource and caches it.
    /// </summary>
    internal static ref readonly IMemoryOwner<byte> GetCopy(in this LazyRawResourceMutableCopy lazy)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (lazy.Copy is null)
            Unsafe.AsRef(lazy.Copy) = lazy.Original.GetMutableCopy();

        return ref lazy.Copy!;
    }
}*/