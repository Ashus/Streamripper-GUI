namespace StreamripperGui.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--verify-xaml"])
        {
            try
            {
                Services.CompiledXamlCheck.Verify(typeof(App).Assembly);
                System.Console.WriteLine("Compiled Avalonia XAML verified.");
            }
            catch (Exception exception)
            {
                System.Console.Error.WriteLine($"Compiled Avalonia XAML verification failed: {exception.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.Length == 2 && args[0] == "--internal-stop" && int.TryParse(args[1], out var processId) && processId > 0)
        {
            Environment.ExitCode = Services.WindowsRecordingProcess.SendStopSignal(processId);
            return;
        }
        var directory = Infrastructure.JsonConfigurationStore.DefaultDirectory(AppContext.BaseDirectory);
        using var instance = Services.SingleInstance.TryAcquire(directory);
        if (instance is null)
        {
            if (!Services.SingleInstance.ActivateExistingAsync(directory).GetAwaiter().GetResult())
                System.Diagnostics.Trace.TraceError("The application is already running, but activation could not be delivered.");
            return;
        }
        App.Instance = instance;
        try { Avalonia.ClassicDesktopStyleApplicationLifetimeExtensions.StartWithClassicDesktopLifetime(BuildAvaloniaApp(), args); }
        finally { App.Instance = null; }
    }

    public static Avalonia.AppBuilder BuildAvaloniaApp() =>
        Avalonia.LoggingExtensions.LogToTrace(
            Avalonia.AppBuilderDesktopExtensions.UsePlatformDetect(Avalonia.AppBuilder.Configure<App>()));
}
