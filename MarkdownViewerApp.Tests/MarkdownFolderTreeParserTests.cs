namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class MarkdownFolderTreeParserTests
{
    [TestMethod]
    public void TryParse_RecognizesNestedProjectStructure()
    {
        const string source = """
            lost-and-found/
            ├── backend/
            │   ├── app/
            │   │   ├── api/       # HTTP routes and error handlers
            │   │   └── storage/   # Database and files
            │   └── requirements.txt
            └── frontend/
                └── package.json
            """;

        var parsed = MarkdownFolderTreeParser.TryParse(source, out var items);

        Assert.IsTrue(parsed);
        Assert.HasCount(8, items);
        Assert.AreEqual(new MarkdownFolderTreeItem("lost-and-found/", null, 0, true), items[0]);
        Assert.AreEqual(new MarkdownFolderTreeItem("backend/", null, 1, true), items[1]);
        Assert.AreEqual(new MarkdownFolderTreeItem("api/", "HTTP routes and error handlers", 3, true), items[3]);
        Assert.AreEqual(new MarkdownFolderTreeItem("requirements.txt", null, 2, false), items[5]);
        Assert.AreEqual(new MarkdownFolderTreeItem("package.json", null, 2, false), items[7]);
    }

    [TestMethod]
    public void TryParse_RejectsOrdinaryTextCodeBlocks()
    {
        Assert.IsFalse(MarkdownFolderTreeParser.TryParse("first line\nsecond line", out _));
    }
}
