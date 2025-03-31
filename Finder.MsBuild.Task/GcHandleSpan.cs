using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Finder.MsBuild.Task;

/// <summary>
/// Provides pseudo-span functionality for GC ref types.
/// </summary>
/// <typeparam name="T">The type of the elements represented by the span.</typeparam>
[PublicAPI]
public ref struct GcHandleSpan<T> : IDisposable
    where T : class
{
    public Span<GCHandle> BackingSpan;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GcHandleSpan(Span<GCHandle> span) => BackingSpan = span;

    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BackingSpan.Length;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Length == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly unsafe ref T Ref(int index)
        => ref Unsafe.AsRef<T>((void*) GCHandle.ToIntPtr(BackingSpan[index]));

    public readonly T? this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Ref(index);
        set
        {
            ref var h = ref BackingSpan[index];
            if (h.IsAllocated) h.Free();
            if (value is not null)
                h = GCHandle.Alloc(value, GCHandleType.Normal);
        }
    }

    public readonly GcHandleSpan<T> this[Range range]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Slice(range);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => value.CopyTo(Slice(range));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void CopyTo(Span<GCHandle> valueBackingSpan)
        => BackingSpan.CopyTo(valueBackingSpan);

    public readonly T? this[Index index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this[index.GetOffset(BackingSpan.Length)];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this[index.GetOffset(BackingSpan.Length)] = value;
    }

    public readonly void Free(int index)
    {
        if ((uint) index >= BackingSpan.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (!BackingSpan[index].IsAllocated)
            BackingSpan[index].Free();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<GCHandle>(GcHandleSpan<T> span)
        => span.BackingSpan;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator GcHandleSpan<T>(Span<GCHandle> span)
        => new(span);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Clear()
    {
        for (var i = 0; i < BackingSpan.Length; i++)
            if (BackingSpan[i].IsAllocated)
                BackingSpan[i].Free();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly GcHandleSpan<T> Slice(int start, int length)
        => new(BackingSpan.Slice(start, length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly GcHandleSpan<T> Slice(Range range)
    {
        var (start, length) = range.GetOffsetAndLength(Length);
        return Slice(start, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly void IDisposable.Dispose() => Clear();

    public readonly Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly GcHandleSpan<T> _span;
        private int _index;

        public Enumerator(GcHandleSpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        public readonly T? Current
        {
            get
            {
                ref readonly var r = ref _span.Ref(_index);
                return Unsafe.IsNullRef(ref Unsafe.AsRef(r)) ? null! : r;
            }
        }

        public bool MoveNext()
        {
            if (_index + 1 >= _span.Length)
                return false;
            _index++;
            return true;
        }
    }
}