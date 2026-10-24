using System.Xml.Linq;

namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class PreviewFolderTreeTemplateTests
{
    [TestMethod]
    public void SidebarFolderTree_DisplaysLabelsAndIconsFromNodeContent()
    {
        var page = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "MainPage.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var tree = page.Descendants(presentation + "TreeView")
            .Single(element => (string?)element.Attribute(x + "Name") == "FolderTree");
        var label = tree.Descendants(presentation + "TextBlock").Single();
        var icon = tree.Descendants(presentation + "FontIcon").Single();

        // RootNodes supplies TreeViewNode as the template context, not FileTreeEntry.
        Assert.AreEqual("{Binding Content.Label}", (string?)label.Attribute("Text"));
        Assert.AreEqual("{Binding Content.Glyph}", (string?)icon.Attribute("Glyph"));
    }

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
