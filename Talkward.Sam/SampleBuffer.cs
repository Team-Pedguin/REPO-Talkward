using System.Buffers;

namespace Talkward.Sam;

/// <summary>
/// A buffer for SAM, to store audio sample data.
/// </summary>
/// <remarks>
/// While this may implement methods usable by syntactic sugar, such as
/// <see cref="IDisposable.Dispose"/>, this implements no interfaces in order
/// to operate within Unity, Mono and other .NET Standard 2.1 environments.
/// </remarks>
[PublicAPI]
public ref struct SampleBuffer<T> where T : unmanaged
{
    private const int ChunkSize = 4096;

    /// <summary>
    /// The currently allocated buffer.
    /// </summary>
    private Span<T> _reserved;

    /// <summary>
    /// The buffer's mutable fraction.
    /// </summary>
    private Span<T> _mutable;

    /// <summary>
    /// An array of samples that exists on the GC heap.
    /// </summary>
    /// <remarks>
    /// Not null if <see cref="_rented"/> is <see langword="true"/>.
    /// If this exists, <see cref="_reserved"/> should point to the same data.
    /// </remarks>
    private T[]? _backing;

    /// <summary>
    /// <see langword="true"/> if the buffer was rented from the <see cref="ArrayPool{T}.Shared"/> pool.
    /// </summary>
    private bool _rented;

    /// <summary>
    /// The amount of samples that have been committed.
    /// </summary>
    private unsafe int CommittedCount
        => (int) (Unsafe.ByteOffset(ref _reserved[0], ref _mutable[0])
                  / (nint) sizeof(T));

    /// <summary>
    /// The span samples that have been committed.
    /// </summary>
    private Span<T> Committed
        => _reserved.Slice(0, CommittedCount);

    /// <summary>
    /// The samples that are mutable and not yet committed.
    /// </summary>
    private Span<T> Mutable
        => _mutable;

    /// <summary>
    /// The samples committed as well as what is currently in the
    /// mutable part of the buffer.
    /// </summary>
    private Span<T> WorkingSet
        => _reserved.Slice(0, CommittedCount + _mutable.Length);

    /// <summary>
    /// All the samples in the buffer; includes committed, mutable and reserved samples.
    /// </summary>
    private Span<T> Reserved
        => _reserved;

    /// <summary>
    /// The amount of data available at the end of the buffer.
    /// </summary>
    /// <remarks>
    /// Note that this is in samples (<see cref="Single"/>s), not <see cref="Byte"/>s.
    /// </remarks>
    private int ReservedCount
        => (int) (Unsafe.ByteOffset(
                      ref _mutable[_mutable.Length - 1],
                      ref _reserved[_reserved.Length - 1])
                  / (nint) sizeof(float));

    /// <summary>
    /// <see langword="true"/> if the buffer is empty.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mutable.IsEmpty && CommittedCount == 0;
    }

    /// <summary>
    /// The target sample space represented by the buffer.
    /// </summary>
    /// <remarks>
    /// Note that this is in samples (<see cref="Single"/>s), not <see cref="Byte"/>s.
    /// </remarks>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mutable.Length;
    }

    /// <summary>
    /// The total sample space represented by the buffer, excluding
    /// any reserved capacity.
    /// </summary>
    /// <remarks>
    /// Note that this is in samples (<see cref="Single"/>s), not <see cref="Byte"/>s.
    /// </remarks>
    public int Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mutable.Length + CommittedCount;
    }


    /// <summary>
    /// The amount of samples that can exist in the buffer
    /// before it needs to be grown.
    /// </summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reserved.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SampleBuffer(Span<T> mutable)
    {
        _mutable = mutable;
        _reserved = mutable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SampleBuffer(int size, bool rented = false)
    {
        _backing = rented ? ArrayPool<T>.Shared.Rent(size) : new T[size];
        _mutable = new Span<T>(_backing, 0, size);
        _reserved = _mutable;
        _rented = rented;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SampleBuffer(T[] data, int size = -1, bool rented = false)
    {
        _backing = data;
        _mutable = new Span<T>(data, 0, size == -1 ? data.Length : size);
        _reserved = _mutable;
        _rented = rented;
    }

    public Span<T> Data
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _mutable;
    }

    /// <summary>
    /// If the buffer is already rented from the <see cref="ArrayPool{Single}.Shared"/>,
    /// returns an <see cref="ArraySegment{Single}"/> that represents the buffer data.
    /// Otherwise, rents a new array from the pool and copies the data into it then returns it.
    /// </summary>
    public ArraySegment<T> Rent()
    {
        if (_rented)
            return new ArraySegment<T>(_backing!, 0, _mutable.Length);

        var rented = ArrayPool<T>.Shared.Rent(_mutable.Length);
        _mutable.CopyTo(rented);
        _backing = rented;
        _mutable = (_reserved = rented).Slice(0, _mutable.Length);
        _rented = true;
        return new ArraySegment<T>(rented, 0, _mutable.Length);
    }

    /// <summary>
    /// Accesses the mutable part of the buffer.
    /// </summary>
    /// <param name="index">The index of the sample to access.</param>
    public ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _mutable[index];
    }

    /// <summary>
    /// Allows for pinning the buffer in memory.
    /// </summary>
    /// <returns>The reference to the first sample in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetPinnableReference()
        => ref _mutable.GetPinnableReference();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(Span<T> destination)
        => WorkingSet.CopyTo(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] destination, int length)
        => WorkingSet.CopyTo(new Span<T>(destination, 0, length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(T[] destination, int offset, int length)
        => WorkingSet.CopyTo(new Span<T>(destination, offset, length));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CopyTo(SampleBuffer<T> destination)
        => WorkingSet.CopyTo(destination.Data);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(SampleBuffer<T> buffer)
        => buffer._mutable;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(SampleBuffer<T> buffer)
        => buffer._mutable;

    /// <summary>
    /// Returns the to the pool if it was rented.
    /// </summary>
    public void Dispose()
    {
        if (_backing != null)
        {
            if (_rented)
                ArrayPool<T>.Shared.Return(_backing);

            _backing = null;
            _rented = false;
        }

        _mutable = default;
        _reserved = default;
    }

    public void Need(int amount)
    {
        if (amount <= 0) return;

        // grow in chunks of ChunkSize
        var newMutableLength = (_mutable.Length + amount + (ChunkSize - 1))
                               / ChunkSize
                               * ChunkSize;

        if (_mutable.Length >= newMutableLength) return;

        var committed = CommittedCount;
        var mutableAvailable = _reserved.Length - committed;
        if (mutableAvailable >= newMutableLength)
        {
            _mutable = _reserved.Slice(committed, newMutableLength);
            return;
        }

        var newData = ArrayPool<T>.Shared.Rent(newMutableLength);
        _mutable.CopyTo(newData);
        _mutable = (_reserved = newData).Slice(0, newMutableLength);
        if (_rented)
            ArrayPool<T>.Shared.Return(_backing!);
        _backing = newData;
        _rented = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAndCommit(T value)
    {
        if (_mutable.Length < 1) Need(1);
        _mutable[0] = value;
        _mutable = _mutable.Slice(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteAndCommit(Span<T> values)
    {
        if (values.Length > _mutable.Length) Need(values.Length);
        values.CopyTo(_mutable);
        _mutable = _mutable.Slice(values.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Commit(int size)
    {
        if (size > _mutable.Length)
            throw new ArgumentOutOfRangeException(nameof(size), "Size is greater than mutable buffer size.");
        _mutable = _mutable.Slice(size);
    }


    public static class Versus<TCheck>
        where TCheck : unmanaged
    {
        public static readonly bool IsSameType
            = typeof(T) == typeof(TCheck);

        public static readonly unsafe bool IsSameSize
            = sizeof(T) == sizeof(TCheck);
    }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ref SampleBuffer<T2> As<T2>()
        where T2 : unmanaged
    {
        if (!Versus<T2>.IsSameSize)
            throw new InvalidCastException($"{typeof(T2).Name} must be the same size as {typeof(T2).Name}.");
        fixed (void* p = &this)
            return ref *(SampleBuffer<T2>*) p;
    }
#pragma warning restore CS8500
}