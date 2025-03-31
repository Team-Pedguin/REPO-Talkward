namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
internal class IgnoresAccessChecksToAttribute : Attribute
{
    public IgnoresAccessChecksToAttribute(string assemblyName)
        => AssemblyName = assemblyName;

    public string AssemblyName { get; }
}