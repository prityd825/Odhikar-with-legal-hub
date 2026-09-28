using System.Text.RegularExpressions;

namespace Odhikar.Infrastructure.ExternalServices.Ai;

/// <summary>Helpers for cleaning LLM output before parsing.</summary>
public static partial class JsonSanitizer
{
    /// <summary>
    /// Removes Markdown code fences (```json ... ```) that models occasionally emit
    /// despite JSON-mode, and trims to the outermost JSON object if extra prose leaks in.
    /// </summary>
    public static string StripCodeFences(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "{}";
        }

        var text = FenceRegex().Replace(raw, string.Empty).Trim();

        var start = text.IndexOf('{');
        if (start < 0) return "{}";

        // Find the balanced matching closing brace for the first '{'
        int depth = 0;
        bool inString = false;
        bool escape = false;
        int end = -1;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }
        }

        if (end > start)
        {
            text = text[start..(end + 1)];
        }
        else
        {
            var lastBrace = text.LastIndexOf('}');
            if (lastBrace > start)
            {
                text = text[start..(lastBrace + 1)];
            }
        }

        return string.IsNullOrWhiteSpace(text) ? "{}" : text;
    }

    [GeneratedRegex("```(?:json)?", RegexOptions.IgnoreCase)]
    private static partial Regex FenceRegex();
}
