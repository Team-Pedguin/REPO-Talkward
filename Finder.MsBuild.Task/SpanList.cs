using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Finder.MsBuild.Task;

/// <summary>
/// This provides an incremental list-building mechanism over a span. 
/// </summary>
/// <remarks>
/// Intended use is from a stack allocated span.
/// <code>
/// SpanList&lt;T&gt; x = stackalloc T[10];
/// </code> 
/// </remarks>
/// <typeparam name="T">The type of the elements in the list.</typeparam>
[PublicAPI]
public ref struct SpanList<T> : IList<T>, IReadOnlyList<T>
{
    public Span<T> BackingSpan;
    public int Count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanList(Span<T> span) => BackingSpan = span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SpanList(Span<T> span, int occupied)
    {
        BackingSpan = span;
        Count = occupied;
    }

    public readonly Span<T> Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[Count..];
    }

    public readonly Span<T> Filled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[..Count];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (Count >= BackingSpan.Length)
            throw new InvalidOperationException("SpanList is full");
        BackingSpan[Count++] = item;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        BackingSpan.Clear();
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
            throw new InvalidOperationException("SpanList is full");

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

    public readonly ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref BackingSpan[index];
    }

    readonly T IList<T>.this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => BackingSpan[index] = value;
    }

    readonly T IReadOnlyList<T>.this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[index];
    }

    public readonly T this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[index.GetOffset(Count)];
    }
    
    public readonly Span<T> this[Range range]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan[range];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            var (offset, length) = range.GetOffsetAndLength(Count);
            value.CopyTo(BackingSpan.Slice(offset, length));
        }
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
    public readonly Span<T>.Enumerator GetEnumerator() => Filled.GetEnumerator();

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

    public static implicit operator Span<T>(SpanList<T> list) => list.Filled;
    public static implicit operator SpanList<T>(Span<T> span) => new(span, span.Length);
}