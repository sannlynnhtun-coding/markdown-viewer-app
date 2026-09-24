namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class MarkdownInlineParserTests
{
    [TestMethod]
    public void Parse_BracketedLinkLabel_HidesTargetAndPreservesLabel()
    {
        const string target = "/book/Adaptive%20Code%20via%20C%23.pdf";
        var segments = MarkdownInlineParser.Parse($"Adaptive Code via C# [[Download]]({target})");

        Assert.HasCount(2, segments);
        Assert.AreEqual(new MarkdownInlineSegment("Adaptive Code via C# "), segments[0]);
        Assert.AreEqual(new MarkdownInlineSegment("[Download]", target), segments[1]);
        Assert.AreEqual("Adaptive Code via C# [Download]", MarkdownInlineParser.GetDisplayText($"Adaptive Code via C# [[Download]]({target})"));
    }

    [TestMethod]
    public void Parse_RegularLink_UsesOnlyItsLabelAsDisplayText()
    {
        var segments = MarkdownInlineParser.Parse("Read the [guide](https://example.com/docs) now");

        Assert.HasCount(3, segments);
        Assert.AreEqual(new MarkdownInlineSegment("Read the "), segments[0]);
        Assert.AreEqual(new MarkdownInlineSegment("guide", "https://example.com/docs"), segments[1]);
        Assert.AreEqual(new MarkdownInlineSegment(" now"), segments[2]);
        Assert.AreEqual("Read the guide now", MarkdownInlineParser.GetDisplayText("Read the [guide](https://example.com/docs) now"));
    }

    [TestMethod]
    public void Parse_TargetWithParentheses_KeepsTheCompleteTarget()
    {
        var segments = MarkdownInlineParser.Parse("[Reference](docs/topic_(advanced).md)");

        Assert.HasCount(1, segments);
        Assert.AreEqual(new MarkdownInlineSegment("Reference", "docs/topic_(advanced).md"), segments[0]);
    }

    [TestMethod]
    public void Parse_UnclosedLink_PreservesSourceText()
    {
        const string source = "See [Download](/book/file.pdf";

        Assert.AreEqual(source, MarkdownInlineParser.GetDisplayText(source));
    }
}
