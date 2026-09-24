using System.Text;

namespace MarkdownViewerApp;

public sealed record MarkdownInlineSegment(string Text, string? LinkTarget = null);

public static class MarkdownInlineParser
{
    public static IReadOnlyList<MarkdownInlineSegment> Parse(string text)
    {
        var segments = new List<MarkdownInlineSegment>();
        var plainText = new StringBuilder();

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '[' ||
                !TryReadLink(text, index, out var label, out var target, out var endIndex))
            {
                plainText.Append(text[index]);
                continue;
            }

            AddPlainText(segments, plainText);
            var displayLabel = CleanFormatting(label);
            if (displayLabel.Length > 0)
            {
                segments.Add(new MarkdownInlineSegment(displayLabel, target));
            }

            index = endIndex;
        }

        AddPlainText(segments, plainText);
        return segments;
    }

    public static string GetDisplayText(string text)
    {
        return string.Concat(Parse(text).Select(segment => segment.Text));
    }

    private static bool TryReadLink(
        string text,
        int startIndex,
        out string label,
        out string target,
        out int endIndex)
    {
        label = "";
        target = "";
        endIndex = startIndex;

        var labelDepth = 1;
        var labelEnd = -1;
        for (var index = startIndex + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == '[')
            {
                labelDepth++;
            }
            else if (text[index] == ']' && --labelDepth == 0)
            {
                labelEnd = index;
                break;
            }
        }

        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var targetDepth = 1;
        var targetStart = labelEnd + 2;
        for (var index = targetStart; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == '(')
            {
                targetDepth++;
            }
            else if (text[index] == ')' && --targetDepth == 0)
            {
                label = text[(startIndex + 1)..labelEnd];
                target = text[targetStart..index].Trim();
                if (target.Length >= 2 && target[0] == '<' && target[^1] == '>')
                {
                    target = target[1..^1];
                }

                endIndex = index;
                return label.Length > 0 && target.Length > 0;
            }
        }

        return false;
    }

    private static void AddPlainText(List<MarkdownInlineSegment> segments, StringBuilder plainText)
    {
        if (plainText.Length == 0)
        {
            return;
        }

        var cleaned = CleanFormatting(plainText.ToString());
        if (cleaned.Length > 0)
        {
            segments.Add(new MarkdownInlineSegment(cleaned));
        }

        plainText.Clear();
    }

    private static string CleanFormatting(string text)
    {
        return text
            .Replace("**", "")
            .Replace("__", "")
            .Replace("`", "");
    }
}
