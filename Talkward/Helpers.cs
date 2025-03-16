using System.Text.Json;

namespace Talkward;

public static class Helpers
{
    public static T Get<T>(this JsonElement element, string name, T defaultValue = default!)
    {
        if (element.TryGetProperty(name, out var property))
        {
            return property.Deserialize<T>() ?? defaultValue;
        }

        return defaultValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo UnsafeBitCast<TFrom, TTo>(TFrom source)
        where TFrom : struct
        where TTo : struct
    {
        if (Unsafe.SizeOf<TFrom>() != Unsafe.SizeOf<TTo>())
            ThrowHelper.Throw<NotSupportedException>();
        return Unsafe.ReadUnaligned<TTo>(ref Unsafe.As<TFrom, byte>(ref source));
    }
}