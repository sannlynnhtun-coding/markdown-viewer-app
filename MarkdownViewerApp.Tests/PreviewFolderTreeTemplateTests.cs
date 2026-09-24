using System.Xml.Linq;

namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class PreviewFolderTreeTemplateTests
{
    [TestMethod]
    public void PreviewFolderTreeTemplate_DisplaysTreeViewNodeContent()
    {
        var pagePath = Path.Combine(AppContext.BaseDirectory, "MainPage.xaml");
        var page = XDocument.Load(pagePath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

        var template = page
            .Descendants(presentation + "DataTemplate")
            .SingleOrDefault(element =>
                string.Equals(
                    (string?)element.Attribute(x + "Key"),
                    "PreviewFolderTreeNodeTemplate",
                    StringComparison.Ordinal));

        Assert.IsNotNull(template, "The preview folder tree must define an item template.");
        var contentPresenter = template.Element(presentation + "ContentPresenter");
        Assert.IsNotNull(contentPresenter);
        Assert.AreEqual("{Binding Content}", (string?)contentPresenter.Attribute("Content"));
    }
}
