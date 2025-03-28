using System.Reflection;
using System.Text;
using FluentAssertions;
using Talkward.Sam;

namespace Talkward.Test;

public class SamResourcesTests
{
    [SetUp]
    public void Setup() => License.Accepted = true;

    [Test]
    public void LengthsNotZero()
    {
        var t = typeof(Resources);

        var members = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var mi in members)
        {
            if (mi is not FieldInfo {IsStatic: true} fi
                || fi.FieldType != typeof(RawResource))
                continue;
            var res = ReflectionHelpers.CreateByRefReadOnlyGetter<RawResource>(fi)();
            var resLength = res.Length;
            TestContext.WriteLine($"{mi.Name}: {res}");
            ((ulong) resLength).Should().NotBe(0uL);
        }
    }

    [Test]
    public void RulesTablesAreBalanced()
    {
        // Test Rules table
        ValidateRulesTable(Resources.Rules, "Rules");

        // Test Rules2 table
        ValidateRulesTable(Resources.Rules2, "Rules2");
    }

    private static unsafe string ExtractRuleString(ReadOnlySpan<byte> rule)
    {
        // convert from ascii to string, strip high bit
        fixed (byte* pRule = rule)
            return string.Create(rule.Length, ((nint) pRule, rule.Length),
                static (s, x) =>
                {
                    var (pRule, ruleLength) = x;
                    var ruleSlice = new Span<byte>((byte*) pRule, ruleLength);
                    /*if ((lastChar & 0x80) == 0)
                        throw new InvalidOperationException("Last character is non-terminating.");*/
                    for (var i = 0; i < ruleSlice.Length-1; i++)
                        s[i] = (char) ruleSlice[i];
                    var lastChar = ruleSlice[^1];
                    s[ruleSlice.Length - 1] = (char) (lastChar & 0x7F);
                });
    }

    private static void ValidateRulesTable(RawResource resource, string tableName)
    {
        var data = resource.Span;

        // Verify specific expected patterns from the original data
        VerifyPattern(data, tableName);

        var openCount = 0;
        var closeCount = 0;
        var ruleIndex = 0; // aka ruleIndex
        var previousWasRuleEnd = true; // before the first rule
        var ruleStart = 0;

        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            var isRuleEnd = (b & 0x80) != 0;

            // Strip high bit to get the actual character
            var c = (byte) (b & 0x7F);

            // Count parentheses
            if (c == (byte) '(') openCount++;
            else if (c == (byte) ')') closeCount++;

            // Check for consecutive high bits (shouldn't happen)
            if (isRuleEnd)
            {
#if DEBUG
                TestContext.WriteLine($"{tableName} #{ruleIndex}: {ExtractRuleString(data.Slice(ruleStart, i - ruleStart + 1))}");
#endif
                var isLastChar = i == data.Length - 1;
                (previousWasRuleEnd && !isLastChar).Should().BeFalse
                ($"{tableName}: Found consecutive rule ends at offset {i}, rule #{ruleIndex}\n{
                    ExtractRuleString(data.Slice(ruleStart, i - ruleStart + 1))}");
                ruleStart = i + 1;
                ruleIndex++;
            }

            // All rules should end with a high bit
            if (c == (byte) ']')
            {
                previousWasRuleEnd.Should().BeTrue
                    ($"{tableName}: Rule marker at offset {i} not preceded by high bit, rule #{ruleIndex}");
            }

            previousWasRuleEnd = isRuleEnd;
        }

        // Each open parenthesis should have a matching closing one
        closeCount.Should().Be(openCount,
            $"{tableName}: Unbalanced parentheses. Open: {openCount}, Close: {closeCount}");

        // There should be a reasonable number of high bits (one per rule)
        ruleIndex.Should().BeGreaterThan(0,
            $"{tableName}: No high bits found");
    }

    private static void VerifyPattern(ReadOnlySpan<byte> data, string tableName)
    {
        switch (tableName)
        {
            case "Rules":
            {
                // expected to start with: ']', 'A'|0x80
                var first = data[0];
                var second = data[1];

                first.Should().Be((byte) ']',
                    "Rules should start with ']'");
                second.Should().Be((byte) 'A' | 0x80,
                    "Second char should be 'A|0x80'");

                // expected to end with: 'j'|0x80
                var last = data[^1];

                last.Should().Be((byte) 'j' | 0x80,
                    "Last char should be 'j|0x80'");
                break;
            }
            case "Rules2":
            {
                // expected to end with: ']', 'A'|0x80
                var secondLast = data[^2];
                var last = data[^1];
                secondLast.Should().Be((byte) ']',
                    "Second to last char should be ']'");
                last.Should().Be((byte) 'A' | 0x80,
                    "Last char should be 'A|0x80'");

                // Find a known sequence, such as the rule for '0': "= ZIYROUW"
                var zeroRuleIndex = data.IndexOf("(0)="u8);

                zeroRuleIndex.Should().NotBe(-1,
                    "Could not find rule for '0' in Rules2");
                break;
            }
            default:
                throw new NotImplementedException
                    ($"Pattern check not implemented for {tableName}");
        }
    }
}