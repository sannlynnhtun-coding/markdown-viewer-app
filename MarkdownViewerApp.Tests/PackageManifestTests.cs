using System.Xml.Linq;

namespace MarkdownViewerApp.Tests;

[TestClass]
public sealed class PackageManifestTests
{
    [TestMethod]
    public void FileTypeAssociation_RegistersStandardMarkdownExtensions()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest");
        var manifest = XDocument.Load(manifestPath);
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

        var registeredExtensions = manifest
            .Descendants(uap + "SupportedFileTypes")
            .Elements(uap + "FileType")
            .Select(element => element.Value)
            .ToArray();

        CollectionAssert.Contains(registeredExtensions, ".md");
        CollectionAssert.Contains(registeredExtensions, ".markdown");
    }
}
