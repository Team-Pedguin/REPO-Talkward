using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal readonly struct Range : IEquatable<Range>
{
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    public static Range All => new(Index.Start, Index.End);

    public Index Start { get; }

    public Index End { get; }

    public static Range StartAt(Index start)
    {
        return new Range(start, Index.End);
    }

    public static Range EndAt(Index end)
    {
        return new Range(Index.Start, end);
    }


    public override bool Equals([NotNullWhen(true)] object value)
    {
        if (value is Range)
        {
            var r = (Range) value;
            if (r.Start.Equals(Start)) return r.End.Equals(End);
        }

        return false;
    }

    public bool Equals(Range other)
    {
        return other.Start.Equals(Start) && other.End.Equals(End);
    }

    public override int GetHashCode()
    {
        return Start.GetHashCode() * 31 + End.GetHashCode();
    }


    public override string ToString()
    {
        return $"{Start}..{End}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var startIndex = Start;
        var isFromEnd = startIndex.IsFromEnd;
        int start;
        if (isFromEnd)
            start = length - startIndex.Value;
        else
            start = startIndex.Value;

        var endIndex = End;
        var isFromEnd2 = endIndex.IsFromEnd;
        int end;
        if (isFromEnd2)
            end = length - endIndex.Value;
        else
            end = endIndex.Value;

        var flag = end > length || start > end;
        if (flag) throw new ArgumentOutOfRangeException(nameof(length));

        return (start, end - start);
    }
}