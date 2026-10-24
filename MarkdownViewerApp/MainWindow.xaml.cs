using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace MarkdownViewerApp;

public sealed partial class MainWindow : Window
{
    public MainWindow(string? initialFilePath = null)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        (AppWindow.Presenter as OverlappedPresenter)?.Maximize();

        RootFrame.Navigate(typeof(MainPage), initialFilePath);
    }
}
