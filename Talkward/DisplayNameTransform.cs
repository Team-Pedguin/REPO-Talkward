using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;

namespace Talkward;

public sealed class DisplayNameTransform
{
    public required string Match { get; set; }

    public required string Replace { get; set; }

    public string? Flags { get; set; }

    public int MaxMatches { get; set; } = 1;

    private AtomicBoolean _init;

    private Regex? _regex;

    [MemberNotNull(nameof(_regex))]
    public Regex Regex
    {
        // ReSharper disable once CognitiveComplexity
        get
        {
            // init lock
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
            if (_init.TrySet())
            {
                var rx = _regex;
                while (rx is null)
                {
                    if (!Thread.Yield())
                        Thread.Sleep(0);
                    rx = _regex;
                    if (rx is not null)
                        return rx;
                }
            }
#pragma warning restore CS8774

            var flags = RegexOptions.Compiled
                        | RegexOptions.IgnoreCase
                        | RegexOptions.CultureInvariant
                        | RegexOptions.ECMAScript;

            var flagsStr = Flags;
            if (string.IsNullOrWhiteSpace(flagsStr))
                return _regex = new(Match, flags);

            // parse flags
            if (flagsStr.Contains('g')) MaxMatches = -1;
            if (flagsStr.Contains('i')) flags |= RegexOptions.IgnoreCase;
            if (flagsStr.Contains('m')) flags |= RegexOptions.Multiline;
            if (flagsStr.Contains('s')) flags |= RegexOptions.Singleline;
            if (flagsStr.Contains('x')) flags |= RegexOptions.IgnorePatternWhitespace;
            if (flagsStr.Contains('n')) flags |= RegexOptions.ExplicitCapture;
            if (flagsStr.Contains('r')) flags |= RegexOptions.RightToLeft;
            return _regex = new(Match, flags);
        }
    }
}