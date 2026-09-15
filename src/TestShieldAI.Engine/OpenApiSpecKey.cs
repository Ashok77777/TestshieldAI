using System.Text.RegularExpressions;

namespace TestShieldAI.Engine;

public static class OpenApiSpecKey
{
    public const string UntitledFallback = "Untitled";

    private static readonly Regex RepeatedWhitespace = new(@"\s+", RegexOptions.Compiled);

    public static string FromTitle(string? title)
    {
        var display = CanonicalDisplay(title);
        return display.Length == 0 ? UntitledFallback : display;
    }

    public static string CanonicalDisplay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return RepeatedWhitespace.Replace(value.Trim(), " ");
    }

    public static string ComparisonKey(string? specKey)
    {
        if (string.IsNullOrWhiteSpace(specKey))
        {
            return "";
        }

        return CanonicalDisplay(specKey).ToUpperInvariant();
    }
}
