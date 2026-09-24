namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class PreviewScrollPositionTests
{
    [TestMethod]
    public void LoadFileAsync_ResetsPreviewScrollToTopAfterRenderingSelectedFile()
    {
        var pageCodePath = Path.Combine(AppContext.BaseDirectory, "MainPage.xaml.cs");
        var pageCode = File.ReadAllText(pageCodePath);
        var loadFileMethod = ReadMethod(pageCode, "private async Task LoadFileAsync(MarkdownFile file)");

        var renderIndex = loadFileMethod.IndexOf("RenderPreview(markdown);", StringComparison.Ordinal);
        var resetIndex = loadFileMethod.IndexOf("ResetPreviewScrollToTop();", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, renderIndex, "Loading a selected file must render its preview.");
        Assert.IsGreaterThan(
            renderIndex,
            resetIndex,
            "Loading a selected file must reset the preview scrollbar after rendering the new content.");

        var resetMethod = ReadMethod(pageCode, "private void ResetPreviewScrollToTop()");
        StringAssert.Contains(resetMethod, "PreviewScrollViewer.ChangeView(null, 0, null, true);");
    }

    private static string ReadMethod(string source, string signature)
    {
        var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, signatureIndex, $"Expected to find method: {signature}");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.IsGreaterThanOrEqualTo(0, openingBraceIndex, $"Expected an opening brace for method: {signature}");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[signatureIndex..(index + 1)];
            }
        }

        Assert.Fail($"Expected a closing brace for method: {signature}");
        return string.Empty;
    }
}
