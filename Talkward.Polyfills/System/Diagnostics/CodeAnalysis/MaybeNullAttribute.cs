namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
internal sealed class MaybeNullAttribute : Attribute;