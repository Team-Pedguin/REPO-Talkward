﻿namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
internal sealed class UnconditionalSuppressMessageAttribute : Attribute
{
    public UnconditionalSuppressMessageAttribute(string category, string checkId)
    {
        Category = category;
        CheckId = checkId;
    }


    public string Category { get; }


    public string CheckId { get; }

    public string? Scope { get; set; }

    public string? Target { get; set; }

    public string? MessageId { get; set; }

    public string? Justification { get; set; }
}