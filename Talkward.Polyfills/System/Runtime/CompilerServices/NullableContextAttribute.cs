using Microsoft.CodeAnalysis;

namespace System.Runtime.CompilerServices;

[CompilerGenerated]
[Embedded]
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Interface |
    AttributeTargets.Delegate, Inherited = false)]
internal sealed class NullableContextAttribute : Attribute
{
    public readonly byte Flag;

    public NullableContextAttribute(byte A_1)
    {
        Flag = A_1;
    }
}