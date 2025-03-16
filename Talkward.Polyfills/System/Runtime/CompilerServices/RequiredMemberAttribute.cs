namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field,
    Inherited = false)]
public sealed class RequiredMemberAttribute : Attribute
{
    public RequiredMemberAttribute()
    {
    }
}