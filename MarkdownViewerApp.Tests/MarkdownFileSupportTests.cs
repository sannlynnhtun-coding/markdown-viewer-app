[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class MarkdownFileSupportTests
{
    [TestMethod]
    [DataRow("README.md")]
    [DataRow("README.MD")]
    [DataRow("release-notes.markdown")]
    [DataRow("release-notes.MARKDOWN")]
    public void IsMarkdownFile_AcceptsStandardMarkdownExtensions(string path)
    {
        Assert.IsTrue(MarkdownFileSupport.IsMarkdownFile(path));
    }

    [TestMethod]
    [DataRow("notes.txt")]
    [DataRow("notes.md.txt")]
    [DataRow("notes")]
    public void IsMarkdownFile_RejectsNonMarkdownExtensions(string path)
    {
        Assert.IsFalse(MarkdownFileSupport.IsMarkdownFile(path));
    }

    [TestMethod]
    public void EnumerateFiles_FindsMarkdownAndMdFilesRecursively()
    {
        var root = Path.Combine(Path.GetTempPath(), $"markdown-viewer-tests-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        try
        {
            File.WriteAllText(Path.Combine(root, "one.md"), "# One");
            File.WriteAllText(Path.Combine(nested, "two.markdown"), "# Two");
            File.WriteAllText(Path.Combine(root, "ignore.txt"), "Ignore");

            var files = MarkdownFileSupport.EnumerateFiles(root)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "one.md", "two.markdown" }, files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
