using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Finder.MsBuild.Task;

/// <summary>
/// This provides an incremental list-building mechanism over a <see cref="GcHandleSpan{T}"/>
/// </summary>
/// <remarks>
/// Intended use is from a stack allocated span.
/// <code>
/// GcHandleSpanList&lt;T&gt; list = stackalloc GCHandle[10];
/// </code> 
/// </remarks>
/// <typeparam name="T">The type of objects referenced by the GCHandles.</typeparam>
[PublicAPI]
public ref struct GcHandleSpanList<T> : IList<T>, IReadOnlyList<T>, IDisposable
    where T : class
{
    public GcHandleSpan<T> BackingSpan;
    public int Count = 0;

    public GcHandleSpanList(Span<GCHandle> span)
    {
        BackingSpan = new GcHandleSpan<T>(span);
        Count = 0;
    }

    public readonly GcHandleSpan<T> Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[Count..];
    }

    public readonly GcHandleSpan<T> Filled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[..Count];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (Count >= BackingSpan.Length)
            throw new InvalidOperationException("GcHandleSpanList is full");
        BackingSpan[Count++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        var span = BackingSpan.BackingSpan;
        for (var i = 0; i < Count; i++)
            span[i].Free();

        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(T item)
    {
        for (var i = 0; i < Count; i++)
            if (EqualityComparer<T>.Default.Equals(BackingSpan[i], item))
                return true;
        return false;
    }

    public readonly void CopyTo(T[] array, int arrayIndex)
    {
        if (array is null)
            throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex + Count > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        for (var i = 0; i < Count; i++)
            array[arrayIndex + i] = BackingSpan[i];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int IndexOf(T item)
    {
        for (var i = 0; i < Count; i++)
            if (EqualityComparer<T>.Default.Equals(BackingSpan[i], item))
                return i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, T item)
    {
        if (unchecked((uint) index) > Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (Count >= BackingSpan.Length)
            throw new InvalidOperationException("GcHandleSpanList is full");

        if (index != Count)
        {
            for (var i = Count; i > index; i--)
                BackingSpan[i] = BackingSpan[i - 1];
            BackingSpan[index] = item;
            Count++;
            return;
        }

        BackingSpan[Count++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(T item)
    {
        for (var i = 0; i < Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(BackingSpan[i], item))
                continue;

            RemoveAt(i);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        if (unchecked((uint) index) >= Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Count--;
        for (var i = index; i < Count; i++)
            BackingSpan[i] = BackingSpan[i + 1];
    }

    public readonly GcHandleSpan<T> this[Range range]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[range];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BackingSpan[range] = value;
    }

    public readonly T this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[index.GetOffset(Count)];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BackingSpan[index.GetOffset(Count)] = value;
    }

    public readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BackingSpan[index] = value;
    }

    readonly int ICollection<T>.Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Count;
    }

    readonly int IReadOnlyCollection<T>.Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Count;
    }

    readonly bool ICollection<T>.IsReadOnly
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly GcHandleSpan<T>.Enumerator GetEnumerator() => BackingSpan.Slice(0, Count).GetEnumerator();

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn,
     Obsolete(
         "Like Span<T>; the IEnumerable interface is not supported as it would require pinning or copying the list.",
         true)]
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();

    [MethodImpl(MethodImplOptions.NoInlining), DoesNotReturn,
     Obsolete(
         "Like Span<T>; the IEnumerable interface is not supported as it would require pinning or copying the list.",
         true)]
    readonly IEnumerator IEnumerable.GetEnumerator() => throw new NotSupportedException();

    public static implicit operator GcHandleSpan<T>(GcHandleSpanList<T> list) => list.BackingSpan.Slice(0, list.Count);
    public static implicit operator GcHandleSpanList<T>(Span<GCHandle> span) => new(span);
    void IDisposable.Dispose() => Clear();
}