namespace PulseBrowser.WinUI;

// Journal de démarrage opt-in (variable PULSE_BROWSER_TRACE_STARTUP=1). Volontairement
// sans dépendance UI : utilisable depuis n'importe quelle couche (coffre inclus) et
// compilable hors du contexte WinUI (tests).
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
