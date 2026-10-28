namespace StreamripperGui.Desktop.Services;

internal sealed class LinuxRecordingProcess : IRecordingProcess
{
    private readonly System.Diagnostics.Process _process;
    private LinuxRecordingProcess(System.Diagnostics.Process process) => _process = process;

    public Stream StandardOutput => _process.StandardOutput.BaseStream;
    public Stream StandardError => _process.StandardError.BaseStream;
    public int Id => _process.Id;
    public bool HasExited => _process.HasExited;
    public int ExitCode => _process.ExitCode;

    public static LinuxRecordingProcess Start(string executable, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Linux recording requires Linux.");
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var process = System.Diagnostics.Process.Start(start) ?? throw new IOException("Could not start Streamripper.");
        return new(process);
    }

    public Task WaitForExitAsync() => _process.WaitForExitAsync();

    public Task RequestStopAsync()
    {
        if (HasExited) return Task.CompletedTask;
        // Signal only this recorder, never the GUI, shell or another recording.
        if (Kill(Id, 2) != 0)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            if (error != 3 && !HasExited) throw new System.ComponentModel.Win32Exception(error);
        }

        return Task.CompletedTask;
    }

    public void ForceStop()
    {
        if (!HasExited) _process.Kill(entireProcessTree: true);
    }

    public void Dispose() => _process.Dispose();

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int processId, int signal);
}