using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Talkward.Test;

public static class ReflectionHelpers
{
    public static ConcurrentDictionary<string, Delegate> _cache = new();

    /// <summary>
    /// Create a by-ref readonly getter for a static field.
    /// </summary>
    public static GetByRefReadOnly<T> CreateByRefReadOnlyGetter<T>(FieldInfo fi)
    {
        if (!fi.IsStatic) throw new InvalidOperationException("Field must be static.");

        var type = fi.DeclaringType ?? throw new InvalidOperationException("Field must have a declaring type.");

        var key = $"$StaticByRefReadOnlyGetter<{type.FullName}.{fi.Name}>";

        return (GetByRefReadOnly<T>)
            _cache.GetOrAdd(key, static (key, fi) =>
            {
                var type = fi.DeclaringType!;
                var dm = new DynamicMethod(key,
                    MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard,
                    fi.FieldType.MakeByRefType(), Type.EmptyTypes, type.Assembly.ManifestModule, true);

                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldsflda, fi);
                il.Emit(OpCodes.Ret);

                return (GetByRefReadOnly<T>) dm.CreateDelegate(typeof(GetByRefReadOnly<T>));
            }, fi);
    }
}

public delegate ref readonly T GetByRefReadOnly<T>();