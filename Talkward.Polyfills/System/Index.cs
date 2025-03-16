using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System;

internal readonly struct Index : IEquatable<Index>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index(int value, bool fromEnd = false)
    {
        var flag = value < 0;
        if (flag) throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");

        if (fromEnd)
            _value = ~value;
        else
            _value = value;
    }

    private Index(int value)
    {
        _value = value;
    }

    public static Index Start => new(0);

    public static Index End => new(-1);

    public int Value
    {
        get
        {
            var flag = _value < 0;
            int num;
            if (flag)
                num = ~_value;
            else
                num = _value;

            return num;
        }
    }

    public bool IsFromEnd => _value < 0;

    public static implicit operator Index(int value)
    {
        return FromStart(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Index FromStart(int value)
    {
        var flag = value < 0;
        if (flag) throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");

        return new Index(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Index FromEnd(int value)
    {
        var flag = value < 0;
        if (flag) throw new ArgumentOutOfRangeException(nameof(value), "value must be non-negative");

        return new Index(~value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOffset(int length)
    {
        var offset = _value;
        var isFromEnd = IsFromEnd;
        if (isFromEnd) offset += length + 1;

        return offset;
    }


    public override bool Equals([NotNullWhen(true)] object other)
    {
        bool flag;
        if (other is Index)
        {
            var index = (Index) other;
            flag = _value == index._value;
        }
        else
        {
            flag = false;
        }

        return flag;
    }

    public bool Equals(Index other)
    {
        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return _value;
    }


    public override string ToString()
    {
        var isFromEnd = IsFromEnd;
        string text;
        if (isFromEnd)
            text = "^" + (uint) Value;
        else
            text = ((uint) Value).ToString();

        return text;
    }

    private readonly int _value;
}