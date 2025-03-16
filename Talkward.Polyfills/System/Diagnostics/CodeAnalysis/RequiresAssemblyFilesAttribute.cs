namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(
    AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event,
    Inherited = false)]
internal sealed class RequiresAssemblyFilesAttribute : Attribute
{
    public RequiresAssemblyFilesAttribute()
    {
    }


    public RequiresAssemblyFilesAttribute(string message)
    {
        Message = message;
    }

    public string? Message { get; }

    public string? Url { get; set; }
}