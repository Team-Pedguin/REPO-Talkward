namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class DoesNotReturnIfAttribute : Attribute
{
    public DoesNotReturnIfAttribute(bool parameterValue)
    {
        ParameterValue = parameterValue;
    }

    public bool ParameterValue { get; }
}