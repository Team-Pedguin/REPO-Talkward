namespace System.Runtime.CompilerServices;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field,
    Inherited = false)]
internal sealed class RequiredMemberAttribute : Attribute
{
    public RequiredMemberAttribute()
    {
    }
}