using System.Text.RegularExpressions;

namespace MarkdownViewerApp;

public sealed record MarkdownFolderTreeItem(
    string Label,
    string? Description,
    int Depth,
    bool IsFolder);

public static partial class MarkdownFolderTreeParser
{
    private static readonly string[] IndentSegments = ["│   ", "|   ", "    "];
    private static readonly string[] BranchSegments = ["├── ", "└── ", "|-- ", "+-- ", "`-- "];

    public static bool TryParse(string text, out IReadOnlyList<MarkdownFolderTreeItem> items)
    {
        var parsed = new List<MarkdownFolderTreeItem>();
        var sawBranch = false;
        var previousDepth = 0;

        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var index = 0;
            var indentDepth = 0;
            while (TryConsume(line, ref index, IndentSegments))
            {
                indentDepth++;
            }

            var hasBranch = TryConsume(line, ref index, BranchSegments);
            if (parsed.Count == 0 && (hasBranch || indentDepth != 0))
            {
                items = Array.Empty<MarkdownFolderTreeItem>();
                return false;
            }

            if (parsed.Count > 0 && !hasBranch)
            {
                items = Array.Empty<MarkdownFolderTreeItem>();
                return false;
            }

            var depth = hasBranch ? indentDepth + 1 : 0;
            if (depth > previousDepth + 1)
            {
                items = Array.Empty<MarkdownFolderTreeItem>();
                return false;
            }

            var content = line[index..].Trim();
            if (content.Length == 0)
            {
                items = Array.Empty<MarkdownFolderTreeItem>();
                return false;
            }

            var descriptionMatch = DescriptionPattern().Match(content);
            var description = descriptionMatch.Success
                ? descriptionMatch.Groups["description"].Value.Trim()
                : null;
            var label = descriptionMatch.Success
                ? content[..descriptionMatch.Index].TrimEnd()
                : content;

            if (label.Length == 0)
            {
                items = Array.Empty<MarkdownFolderTreeItem>();
                return false;
            }

            parsed.Add(new MarkdownFolderTreeItem(
                label,
                description,
                depth,
                label.EndsWith("/", StringComparison.Ordinal) ||
                label.EndsWith('\\')));
            sawBranch |= hasBranch;
            previousDepth = depth;
        }

        items = parsed;
        return sawBranch && parsed.Count >= 2;
    }

    private static bool TryConsume(string line, ref int index, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (line.AsSpan(index).StartsWith(candidate, StringComparison.Ordinal))
            {
                index += candidate.Length;
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\s+#\s+(?<description>.+)$")]
    private static partial Regex DescriptionPattern();
}
