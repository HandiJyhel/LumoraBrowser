using Microsoft.UI.Xaml;

namespace PulseBrowser.WinUI;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        WinUiRuntimeTrace.Write("App constructor start");
        InitializeComponent();
        WinUiRuntimeTrace.Write("App constructor after InitializeComponent");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        WinUiRuntimeTrace.Write("OnLaunched start");
        _window = new MainWindow();
        WinUiRuntimeTrace.Write("MainWindow constructed");
        _window.Activate();
        WinUiRuntimeTrace.Write("MainWindow activated");
        ((MainWindow)_window).InitializeBrowserSurface();
        WinUiRuntimeTrace.Write("Browser surface initialized");
    }
}
