using System.Linq;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.Activation;

namespace MarkdownViewerApp;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        var initialFilePath = activationArgs?.Kind == ExtendedActivationKind.File &&
                              activationArgs.Data is IFileActivatedEventArgs fileArgs
            ? fileArgs.Files.FirstOrDefault()?.Path
            : null;

        MainWindow = new MainWindow(initialFilePath);
        MainWindow.Activate();
    }
}
