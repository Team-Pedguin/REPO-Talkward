using System;
using System.Text.RegularExpressions;
using NuGet.Versioning;

namespace Finder.MsBuild.Task;

public partial class IdentifyCauseOfAssemblyReference
{
    private class AssemblyRefMatch
    {
        private const RegexOptions DefaultRegexFlags =
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript;

        public string? ExactName { get; }
        public Regex? NameRegex { get; }
        public Regex? VersionRegex { get; }
        public VersionRange? VersionSpec { get; }

        public AssemblyRefMatch(string nameExpression, string? versionExpression)
        {
            if (nameExpression.StartsWith('/') && nameExpression.EndsWith('/'))
            {
                NameRegex = new Regex(nameExpression[1..^1], DefaultRegexFlags);
                ExactName = null;
            }
            else
            {
                if (nameExpression.Contains('*'))
                {
                    var pattern = $"^{Regex.Escape(nameExpression).Replace("\\*", ".*")}$";
                    NameRegex = new Regex(pattern, DefaultRegexFlags);
                    ExactName = null;
                }
                else
                {
                    ExactName = nameExpression;
                    NameRegex = null;
                }
            }

            if (string.IsNullOrWhiteSpace(versionExpression))
            {
                VersionRegex = null;
                VersionSpec = null;
            }
            else if (versionExpression.StartsWith('/') && versionExpression.EndsWith('/'))
            {
                VersionRegex = new Regex(versionExpression[1..^1], DefaultRegexFlags);
                VersionSpec = new VersionRange();
            }
            else
            {
                if (versionExpression.Contains('*'))
                {
                    var pattern = $"^{Regex.Escape(versionExpression).Replace("\\*", ".*")}$";
                    VersionRegex = new Regex(pattern, DefaultRegexFlags);
                    VersionSpec = null;
                }
                else
                {
                    VersionRegex = null;
                    VersionSpec = VersionRange.Parse(versionExpression);
                }
            }
        }

        //@formatter:off
        public bool IsMatch(string assemblyName, NuGetVersion version)
            => (ExactName == null || string.Equals(assemblyName, ExactName, StringComparison.Ordinal))
            && (NameRegex == null || NameRegex.IsMatch(assemblyName))
            && (VersionRegex == null || VersionRegex.IsMatch(version.ToString()))
            && (VersionSpec == null || VersionSpec.Satisfies(version, VersionComparison.Default));
        //@formatter:on

        public override string ToString()
        {
            var name = ExactName ?? NameRegex?.ToString() ?? "<unknown>";
            var version = VersionSpec?.ToString() ?? VersionRegex?.ToString();
            return version is null
                ? $"{name}"
                : $"{name}, {version}";
        }
    }
}