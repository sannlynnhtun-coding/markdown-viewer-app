using System.IO;

namespace MarkdownViewerApp;

public static class MarkdownFileSupport
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown"
    };

    public static bool IsMarkdownFile(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public static IEnumerable<string> EnumerateFiles(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .Where(IsMarkdownFile);
    }
}
