using System.Buffers;
using System.Reflection;
using UnityEngine;

namespace Talkward.Sam;

/// <summary>
/// Raw accessor of a resource in a manifest assembly.
/// </summary>
/// <remarks>
/// Contains a pointer to a read-only memory region inside a mapped assembly file.
/// It will not be discarded or moved for the lifetime of the assembly.
/// The expected usage is for assemblies to access their own resources and thus
/// their lifetimes should be identical.
/// </remarks>
/// <param name="start">The pointer to the start of the resource.</param>
/// <param name="length">The length of the resource.</param>
[PublicAPI]
public readonly unsafe struct RawResource(byte* start, nuint length)
    : IEquatable<RawResource>, IComparable<RawResource>
{
    /// <summary>
    /// Pointer to the start of the resource.
    /// </summary>
    public byte* Start => start;

    /// <summary>
    /// Length of the resource in bytes.
    /// </summary>
    public nuint Length => length;


    /// <summary>
    /// Representation of the resource as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <exception cref="OverflowException">If the resource is too large to fit in a single span.</exception>
    public ReadOnlySpan<byte> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => length > int.MaxValue
            ? OutOfRange()
            : new(start, (int) length);
    }

    public IMemoryOwner<byte> GetMutableCopy()
    {
        var owner = MemoryPool<byte>.Shared.Rent((int) Length);
        var bytes = owner.Memory.Span;
        Span.CopyTo(bytes);
        return owner;
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private ReadOnlySpan<byte> OutOfRange()
        => throw new OverflowException("Resource length exceeds maximum size for a span.");

    public ref readonly byte this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) index >= Length)
                ThrowIndexOutOfRangeException();
            return ref Unsafe.Add(ref *Start, index);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(RawResource value)
        => value.Span;

    /// <summary>
    /// Validate the resource against the assembly of the specified type.
    /// </summary>
    /// <remarks>
    /// This is useful for validating the resource against the assembly,
    /// it's lifetime and so-forth.
    /// </remarks>
    /// <param name="type">The type within an assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns><see langref="true"/> if the resource matches the assembly and name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateAgainst(Type type, string name)
        => ValidateAgainst(type.Assembly, name);

    /// <summary>
    /// Validate the resource against the specified assembly.
    /// </summary>
    /// <remarks>
    /// This is useful for validating the resource against the assembly,
    /// it's lifetime and so-forth.
    /// </remarks>
    /// <param name="assembly">The assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns><see langref="true"/> if the resource matches the assembly and name.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ValidateAgainst(Assembly assembly, string name)
    {
        try
        {
            var check = RawResources.Get(assembly, name);
            return this == check;
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RawResource other)
        => Start == other.Start && Length == other.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
        => obj is RawResource other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => HashCode.Combine((nint) Start, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RawResource a, RawResource b)
        => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RawResource a, RawResource b)
        => !(a == b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(RawResource other)
    {
        var cmpStart = (nint) Start - (nint) other.Start;
        if (cmpStart != 0)
            return cmpStart > 0 ? 1 : -1;

        var cmpLength = Length - other.Length;
        return cmpLength == 0 ? 0 : cmpLength > 0 ? 1 : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(RawResource left, RawResource right)
        => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(RawResource left, RawResource right)
        => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(RawResource left, RawResource right)
        => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(RawResource left, RawResource right)
        => left.CompareTo(right) >= 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override string ToString()
        => $"[RawResource {Length} bytes @ 0x{(ulong) (nint) Start:X8})]";
}

[PublicAPI]
public readonly unsafe struct RawResource<T>(RawResource bytes)
    : IEquatable<RawResource<T>>, IComparable<RawResource<T>>
    where T : unmanaged
{
    private static nuint SizeOfType => (nuint) sizeof(T);

    private static string TypeName => typeof(T).FullName ?? typeof(T).Name;

    public RawResource Bytes => bytes;

    /// <summary>
    /// Pointer to the start of the resource.
    /// </summary>
    public T* Start => (T*) bytes.Start;

    /// <summary>
    /// Length of the resource in bytes.
    /// </summary>
    public nuint Length => bytes.Length / SizeOfType;

    /// <summary>
    /// Representation of the resource as a <see cref="ReadOnlySpan{Byte}"/>.
    /// </summary>
    /// <exception cref="OverflowException">If the resource is too large to fit in a single span.</exception>
    public ReadOnlySpan<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Length > int.MaxValue
            ? OutOfRange()
            : new(Start, (int) Length);
    }

    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint) index >= Length)
                ThrowIndexOutOfRangeException();
            return ref Unsafe.Add(ref *Start, index);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private static void ThrowIndexOutOfRangeException()
        => throw new IndexOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn, StackTraceHidden, HideInCallstack]
    private ReadOnlySpan<T> OutOfRange()
        => throw new OverflowException("Resource length exceeds maximum size for a span.");


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(RawResource<T> value)
        => value.Span;

    /// <summary>
    /// Validate the resource against the assembly of the specified type.
    /// </summary>
    /// <remarks>
    /// This is useful for validating the resource against the assembly,
    /// it's lifetime and so-forth.
    /// </remarks>
    /// <param name="type">The type within an assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns><see langref="true"/> if the resource matches the assembly and name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ValidateAgainst(Type type, string name)
        => ValidateAgainst(type.Assembly, name);

    /// <summary>
    /// Validate the resource against the specified assembly.
    /// </summary>
    /// <remarks>
    /// This is useful for validating the resource against the assembly,
    /// it's lifetime and so-forth.
    /// </remarks>
    /// <param name="assembly">The assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns><see langref="true"/> if the resource matches the assembly and name.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool ValidateAgainst(Assembly assembly, string name)
    {
        try
        {
            var check = RawResources.Get(assembly, name);
            return Bytes == check;
        }
        catch
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator RawResource(RawResource<T> value)
        => value.Bytes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator RawResource<T>(RawResource value)
        => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(RawResource<T> other)
        => Start == other.Start && Length == other.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj)
        => obj is RawResource<T> other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
        => HashCode.Combine((nint) Start, Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(RawResource<T> a, RawResource<T> b)
        => a.Equals(b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(RawResource<T> a, RawResource<T> b)
        => !(a == b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(RawResource<T> other)
    {
        var cmpStart = (nint) Start - (nint) other.Start;
        if (cmpStart != 0)
            return cmpStart > 0 ? 1 : -1;

        var cmpLength = Length - other.Length;
        return cmpLength == 0 ? 0 : cmpLength > 0 ? 1 : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(RawResource<T> left, RawResource<T> right)
        => left.CompareTo(right) < 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(RawResource<T> left, RawResource<T> right)
        => left.CompareTo(right) > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(RawResource<T> left, RawResource<T> right)
        => left.CompareTo(right) <= 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(RawResource<T> left, RawResource<T> right)
        => left.CompareTo(right) >= 0;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override string ToString()
        => $"[RawResource {TypeName} x{Length} @ 0x{(ulong) (nint) Start:X8})]";
}

[PublicAPI]
public static class RawResourceHelpers
{
    /// <summary>
    /// Get a raw resource from the assembly of the specified type.
    /// </summary>
    /// <param name="type">The type within an assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RawResource GetRawResource(this Type type, string name)
        => RawResources.Get(type, name);

    /// <summary>
    /// Get a raw resource from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly that holds the resource.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>A <see cref="RawResource"/> representing the resource.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RawResource GetRawResource(this Assembly assembly, string name)
        => RawResources.Get(assembly, name);
}