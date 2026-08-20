using Microsoft.UI.Xaml;

namespace MarkdownViewerApp;

public sealed partial class MainWindow : Window
{
    public MainWindow(string? initialFilePath = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(MainPage), initialFilePath);
    }
}
