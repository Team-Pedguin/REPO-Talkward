﻿namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.ReturnValue,
    AllowMultiple = true)]
internal sealed class NotNullIfNotNullAttribute : Attribute
{
    public NotNullIfNotNullAttribute(string parameterName)
    {
        ParameterName = parameterName;
    }

    public string ParameterName { get; }
}