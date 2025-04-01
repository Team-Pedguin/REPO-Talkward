using System.Reflection;
using System.Reflection.Emit;

namespace Talkward;

[PublicAPI]
public static class RefList
{
    internal static readonly FieldInfo DynamicMethodReturnTypeField =
        typeof(DynamicMethod).GetField("returnType", BindingFlags.NonPublic | BindingFlags.Instance) ??
        typeof(DynamicMethod).GetField("_returnType", BindingFlags.NonPublic | BindingFlags.Instance) ??
        typeof(DynamicMethod).GetField("m_returnType", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Cannot find returnType field on DynamicMethod");

}
public delegate ref TResult RefFunc<in T, TResult>(T a);

[PublicAPI]
public sealed class RefList<T> : List<T>
{
    static RefList()
    {
        var objListType = typeof(List<>).MakeGenericType(typeof(T));
        var itemsField = objListType.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
        if (itemsField == null)
            throw new PlatformNotSupportedException("Unable to find _items field in List<object>.");
        var dynMethod = new DynamicMethod(
            $"GetItemsRef_List_{typeof(T).FullName}",
            typeof(void),
            [typeof(List<T>)],
            objListType,
            true);
        RefList.DynamicMethodReturnTypeField.SetValue(dynMethod, typeof(T[]).MakeByRefType());
        var ilGenerator = dynMethod.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Ldflda, itemsField);
        ilGenerator.Emit(OpCodes.Ret);
        GetInternalArray = (RefFunc<List<T>, T[]>)
                dynMethod.CreateDelegate(typeof(RefFunc<List<T>, T[]>));
    }

    public static RefFunc<List<T>, T[]> GetInternalArray;

    public RefList()
    {
    }

    public RefList(int capacity) : base(capacity)
    {
    }

    public RefList(IEnumerable<T> collection) : base(collection)
    {
    }

    public RefList(List<T> list) : base(list)
    {
    }
    
    public ref T[] Array
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref GetInternalArray(this);
    } 

    public new ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[long index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[nint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[uint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[ulong index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[ushort index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[byte index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }

    public ref T this[sbyte index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Array[index];
    }
}