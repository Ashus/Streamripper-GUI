namespace StreamripperGui.Desktop.Services;

/// <summary>A private hidden console keeps stop signals isolated from Rider and other recordings.</summary>
internal sealed class WindowsRecordingProcess : IRecordingProcess
{
    private readonly Microsoft.Win32.SafeHandles.SafeProcessHandle _process;
    public Stream StandardOutput { get; }
    public Stream StandardError { get; }
    public int Id { get; }
    public bool HasExited => WaitForSingleObject(_process, 0) == 0;
    public int ExitCode
    {
        get { Check(GetExitCodeProcess(_process, out var code)); return unchecked((int)code); }
    }

    private WindowsRecordingProcess(Microsoft.Win32.SafeHandles.SafeProcessHandle process, int id, Stream output, Stream error)
    {
        _process = process;
        Id = id;
        StandardOutput = output;
        StandardError = error;
    }

    public static WindowsRecordingProcess Start(string executable, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException(Localization.Localizer.Current.Message("WindowsRecordingOnly"));
        var security = new SecurityAttributes { Length = System.Runtime.InteropServices.Marshal.SizeOf<SecurityAttributes>(), Inherit = true };
        Check(CreatePipe(out var outputRead, out var outputWrite, ref security, 0));
        using (outputWrite)
        {
            try
            {
                Check(CreatePipe(out var errorRead, out var errorWrite, ref security, 0));
                using (errorWrite)
                {
                    try
                    {
                        Check(CreatePipe(out var inputRead, out var inputWrite, ref security, 0));
                        using (inputRead)
                        using (inputWrite)
                        {
                            Check(SetHandleInformation(outputRead, 1, 0));
                            Check(SetHandleInformation(errorRead, 1, 0));
                            Check(SetHandleInformation(inputWrite, 1, 0));
                            var startup = new StartupInfo
                            {
                                Size = System.Runtime.InteropServices.Marshal.SizeOf<StartupInfo>(),
                                Flags = 0x101, // STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW, SW_HIDE.
                                Input = inputRead.DangerousGetHandle(),
                                Output = outputWrite.DangerousGetHandle(),
                                Error = errorWrite.DangerousGetHandle()
                            };
                            var command = new System.Text.StringBuilder(string.Join(" ", new[] { executable }.Concat(arguments).Select(QuoteArgument)));
                            Check(CreateProcess(executable, command, 0, 0, true, 0x10, 0,
                                AppContext.BaseDirectory, ref startup, out var info)); // CREATE_NEW_CONSOLE.
                            CloseHandle(info.Thread);
                            var process = new Microsoft.Win32.SafeHandles.SafeProcessHandle(info.Process, ownsHandle: true);
                            return new WindowsRecordingProcess(process, info.ProcessId,
                                new FileStream(outputRead, FileAccess.Read),
                                new FileStream(errorRead, FileAccess.Read));
                        }
                    }
                    catch { errorRead.Dispose(); throw; }
                }
            }
            catch { outputRead.Dispose(); throw; }
        }
    }

    // Windows CRT quoting, including trailing backslashes and embedded quotes.
    internal static string QuoteArgument(string value)
    {
        var result = new System.Text.StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\') { slashes++; continue; }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    public Task WaitForExitAsync() => Task.Run(() =>
    {
        if (WaitForSingleObject(_process, uint.MaxValue) != 0)
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    });

    public async Task RequestStopAsync()
    {
        if (HasExited) return;
        // A short-lived copy attaches to the private console. The GUI never changes consoles.
        var helperPath = Environment.ProcessPath ?? throw new InvalidOperationException(Localization.Localizer.Current.Message("ExecutablePathUnavailable"));
        var start = new System.Diagnostics.ProcessStartInfo(helperPath) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(helperPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(WindowsRecordingProcess).Assembly.Location);
        start.ArgumentList.Add("--internal-stop");
        start.ArgumentList.Add(Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var helper = System.Diagnostics.Process.Start(start) ?? throw new IOException(Localization.Localizer.Current.Message("StopHelperStartFailed"));
        await helper.WaitForExitAsync();
        if (helper.ExitCode != 0 && !HasExited)
            throw new IOException(Localization.Localizer.Current.Message("StopSignalFailed", helper.ExitCode));
    }

    public void ForceStop()
    {
        if (!HasExited) Check(TerminateProcess(_process, 1));
    }

    public static int SendStopSignal(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0) return 1;
        FreeConsole();
        if (!AttachConsole(processId)) return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        ConsoleHandler handler = _ => true;
        try
        {
            if (!SetConsoleCtrlHandler(handler, true) || !GenerateConsoleCtrlEvent(1, 0))
                return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            // Keep the helper alive until its own control-event callback has been dispatched.
            Thread.Sleep(100);
            GC.KeepAlive(handler);
            return 0;
        }
        finally { FreeConsole(); }
    }

    public void Dispose()
    {
        StandardOutput.Dispose();
        StandardError.Dispose();
        _process.Dispose();
    }

    private static void Check(bool success)
    {
        if (!success) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public nint Descriptor;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] public bool Inherit;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public short Show, ReservedSize;
        public nint ReservedPointer, Input, Output, Error;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public nint Process, Thread;
        public int ProcessId, ThreadId;
    }

    private delegate bool ConsoleHandler(uint signal);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CreatePipe(out Microsoft.Win32.SafeHandles.SafeFileHandle read, out Microsoft.Win32.SafeHandles.SafeFileHandle write, ref SecurityAttributes security, int size);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetHandleInformation(Microsoft.Win32.SafeHandles.SafeFileHandle handle, uint mask, uint flags);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, System.Text.StringBuilder command, nint processSecurity, nint threadSecurity, bool inherit, uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInformation process);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleHandler handler, bool add);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(Microsoft.Win32.SafeHandles.SafeProcessHandle process, uint milliseconds);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(Microsoft.Win32.SafeHandles.SafeProcessHandle process, out uint exitCode);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(Microsoft.Win32.SafeHandles.SafeProcessHandle process, uint exitCode);
}
