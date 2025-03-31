using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Finder.MsBuild.Task;

[PublicAPI]
public static class StringHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("ReSharper", "CognitiveComplexity")]
    public static unsafe string Join(this GcHandleSpan<string> values, char separator)
    {
        if (values.IsEmpty)
            return "";

        var length = 0;
        foreach (var value in values)
            length += value is null ? 0 : value.Length + 1;
        length--; // remove last separator

        fixed (GCHandle* p = values.BackingSpan)
            return string.Create(length,
                ((nint) p, values.Length, separator),
                static (chars, x) =>
                {
                    var (p, l, separator) = x;
                    var handles = new ReadOnlySpan<GCHandle>((GCHandle*) p, l);
                    for (var i = 0; i < handles.Length; i++)
                    {
                        if (!handles[i].IsAllocated) continue;
                        var value = (string) handles[i].Target;
                        value.AsSpan().CopyTo(chars);
                        if (i == handles.Length - 1)
                            break;
                        chars[value.Length] = separator;
                        chars = chars[(value.Length + 1)..];
                    }
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Join(this GcHandleSpanList<string> values, char separator)
        => values.Filled.Join(separator);
}