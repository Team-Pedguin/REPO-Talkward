namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
internal sealed class NotNullAttribute : Attribute;