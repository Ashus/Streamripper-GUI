namespace StreamripperGui.Desktop.Services;

internal static class RecordingProcess
{
    public static string QuoteArgument(string value) => OperatingSystem.IsWindows()
        ? WindowsRecordingProcess.QuoteArgument(value)
        : "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    public static IRecordingProcess Start(string executable, IReadOnlyList<string> arguments)
    {
        if (OperatingSystem.IsWindows()) return WindowsRecordingProcess.Start(executable, arguments);
        if (OperatingSystem.IsLinux()) return LinuxRecordingProcess.Start(executable, arguments);
        throw new PlatformNotSupportedException("Recording is supported on Windows and Linux only.");
    }
}