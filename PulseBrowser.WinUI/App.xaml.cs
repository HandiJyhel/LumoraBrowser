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

internal static class WinUiRuntimeTrace
{
    private static readonly string TraceFile = Path.Combine(AppContext.BaseDirectory, "winui-runtime-trace.log");

    public static void Write(string message)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("PULSE_BROWSER_TRACE_STARTUP") != "1")
            {
                return;
            }

            File.AppendAllText(TraceFile, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Startup tracing must never become another startup failure.
        }
    }
}
