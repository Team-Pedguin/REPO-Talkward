namespace System.Runtime.CompilerServices;

internal static class RuntimeHelpers2
{
    public static T[] GetSubArray<T>(T[] array, Range range)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        var flag = array == null;
        if (flag) throw new ArgumentNullException(nameof(array));

        var offsetAndLength = range.GetOffsetAndLength(array!.Length);
        var offset = offsetAndLength.Item1;
        var length = offsetAndLength.Item2;
        var flag2 = default(T) != null || typeof(T[]) == array.GetType();
        T[] dest;
        if (flag2)
        {
            var flag3 = length == 0;
            if (flag3) return Empty<T>.Array;

            dest = new T[length];
        }
        else
        {
            dest = (T[]) Array.CreateInstance(array.GetType().GetElementType(), length);
        }

        Array.Copy(array, offset, dest, 0, length);
        return dest;
    }

    private static class Empty<T>
    {
        public static readonly T[] Array = new T[0];
    }
}