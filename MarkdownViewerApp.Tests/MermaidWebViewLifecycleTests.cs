namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class MermaidWebViewLifecycleTests
{
    [TestMethod]
    public async Task NavigateAsync_InitializesCoreBeforeNavigating()
    {
        var calls = new List<string>();

        await MermaidWebViewLifecycle.NavigateAsync(
            () =>
            {
                calls.Add("ensure");
                return Task.CompletedTask;
            },
            _ =>
            {
                calls.Add("navigate");
                if (calls[0] != "ensure")
                {
                    throw new InvalidOperationException(
                        "NavigateToString(): Failed because a valid CoreWebView2 is not present.");
                }
            },
            "<html></html>");

        CollectionAssert.AreEqual(new[] { "ensure", "navigate" }, calls);
    }
}
