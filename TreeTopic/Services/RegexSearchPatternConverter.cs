using System.Text;
using System.Text.RegularExpressions;

namespace TreeTopic.Services;

public sealed record RegexSearchSpec(
    string Pattern,
    bool CaseSensitive,
    string PostgresOperator,
    string MySqlMatchType);

public interface IRegexSearchPatternConverter
{
    RegexSearchSpec Convert(string rawPattern, bool caseSensitive, string providerName);
}

public class RegexSearchPatternConverter : IRegexSearchPatternConverter
{
    public RegexSearchSpec Convert(string rawPattern, bool caseSensitive, string providerName)
    {
        if (string.IsNullOrWhiteSpace(rawPattern))
        {
            throw new ArgumentException("Search pattern is required.", nameof(rawPattern));
        }

        var input = rawPattern.Trim();
        var pattern = input;
        var flags = string.Empty;

        if (TryParseJsRegexLiteral(input, out var parsedPattern, out var parsedFlags))
        {
            pattern = parsedPattern;
            flags = parsedFlags;
        }

        var flagSet = new HashSet<char>(flags.Select(char.ToLowerInvariant));
        foreach (var flag in flagSet)
        {
            if (flag is not ('i' or 'm' or 's' or 'x' or 'c'))
            {
                throw new ArgumentException($"Unsupported regex flag: '{flag}'", nameof(rawPattern));
            }
        }

        // Support inline flags where DB engines generally accept PCRE-style syntax.
        var inlineFlags = new StringBuilder();
        if (flagSet.Contains('m')) inlineFlags.Append('m');
        if (flagSet.Contains('s')) inlineFlags.Append('s');
        if (flagSet.Contains('x')) inlineFlags.Append('x');
        if (inlineFlags.Length > 0)
        {
            pattern = $"(?{inlineFlags}){pattern}";
        }

        var effectiveCaseSensitive = caseSensitive;
        if (flagSet.Contains('i')) effectiveCaseSensitive = false;
        if (flagSet.Contains('c')) effectiveCaseSensitive = true;

        // Validate syntax early.
        _ = new Regex(
            pattern,
            effectiveCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(250));

        var provider = providerName?.ToLowerInvariant() ?? string.Empty;
        var postgresOperator = effectiveCaseSensitive ? "~" : "~*";
        var mySqlMatchType = effectiveCaseSensitive ? "c" : "i";

        if (provider.Contains("mysql"))
        {
            return new RegexSearchSpec(pattern, effectiveCaseSensitive, postgresOperator, mySqlMatchType);
        }

        // Default to PostgreSQL-compatible behavior.
        return new RegexSearchSpec(pattern, effectiveCaseSensitive, postgresOperator, mySqlMatchType);
    }

    private static bool TryParseJsRegexLiteral(string input, out string pattern, out string flags)
    {
        pattern = input;
        flags = string.Empty;

        if (input.Length < 2 || input[0] != '/')
        {
            return false;
        }

        var closingSlashIndex = FindLastUnescapedSlash(input);
        if (closingSlashIndex <= 0)
        {
            return false;
        }

        pattern = input[1..closingSlashIndex];
        flags = input[(closingSlashIndex + 1)..];
        return true;
    }

    private static int FindLastUnescapedSlash(string value)
    {
        for (var i = value.Length - 1; i > 0; i--)
        {
            if (value[i] != '/')
            {
                continue;
            }

            var backslashCount = 0;
            for (var j = i - 1; j >= 0 && value[j] == '\\'; j--)
            {
                backslashCount++;
            }

            // Even number of backslashes means slash is not escaped.
            if (backslashCount % 2 == 0)
            {
                return i;
            }
        }

        return -1;
    }
}
