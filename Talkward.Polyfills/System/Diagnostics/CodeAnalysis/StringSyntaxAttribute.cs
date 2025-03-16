namespace System.Diagnostics.CodeAnalysis;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class StringSyntaxAttribute : Attribute
{
    public const string CompositeFormat = "CompositeFormat";

    public const string DateOnlyFormat = "DateOnlyFormat";

    public const string DateTimeFormat = "DateTimeFormat";

    public const string EnumFormat = "EnumFormat";

    public const string GuidFormat = "GuidFormat";

    public const string Json = "Json";

    public const string NumericFormat = "NumericFormat";

    public const string Regex = "Regex";

    public const string TimeOnlyFormat = "TimeOnlyFormat";

    public const string TimeSpanFormat = "TimeSpanFormat";

    public const string Uri = "Uri";

    public const string Xml = "Xml";


    private static readonly object[] _emptyArguments = new object[0];

    public StringSyntaxAttribute(string syntax)
    {
        Syntax = syntax;
        Arguments = _emptyArguments;
    }

    public StringSyntaxAttribute(string syntax, params object[] arguments)
    {
        Syntax = syntax;
        Arguments = arguments;
    }

    public string Syntax { get; }


    public object[] Arguments { get; }
}