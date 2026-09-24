namespace MarkdownViewerApp;

public static class MermaidWebViewLifecycle
{
    public static async Task NavigateAsync(
        Func<Task> ensureCoreWebViewAsync,
        Action<string> navigateToString,
        string html)
    {
        await ensureCoreWebViewAsync();
        navigateToString(html);
    }
}
