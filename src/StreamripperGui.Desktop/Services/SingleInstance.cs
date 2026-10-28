namespace StreamripperGui.Desktop.Services;

/// <summary>Locks the configuration directory before loading it and forwards activation over a private pipe.</summary>
public sealed class SingleInstance : IDisposable
{
    private readonly FileStream _lock;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _listener;
    private Action? _activate;
    private int _pending;
    private readonly string _pipeName;

    private SingleInstance(FileStream file, string directory)
    {
        _lock = file;
        _pipeName = PipeName(directory);
        _listener = ListenAsync();
    }

    public static SingleInstance? TryAcquire(string directory)
    {
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) return TryAcquireLinux(directory);
        FileStream file;
        try
        {
            // Delete together with releasing the handle; never delete another instance's newly acquired lock.
            file = new FileStream(Path.Combine(directory, ".instance.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException exception) when (exception.HResult == unchecked((int)0x80070020)) { return null; }
        try { return new SingleInstance(file, directory); }
        catch { file.Dispose(); throw; }
    }

    private static SingleInstance? TryAcquireLinux(string directory)
    {
        // Keep a stable inode: unlinking a locked file allows a second owner on Unix.
        var descriptor = OpenLinuxLock(Path.Combine(directory, ".instance.lock"), 2 | 64 | 0x80000, 0x180);
        if (descriptor < 0) throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
        var file = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(descriptor, ownsHandle: true), FileAccess.ReadWrite);
        if (Flock(file.SafeFileHandle, 2 | 4) != 0)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            file.Dispose();
            if (error == 11) return null;
            throw new System.ComponentModel.Win32Exception(error);
        }
        try { return new SingleInstance(file, directory); }
        catch { file.Dispose(); throw; }
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int Flock(Microsoft.Win32.SafeHandles.SafeFileHandle file, int operation);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenLinuxLock(string path, int flags, uint mode);

    private static string PipeName(string directory)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (OperatingSystem.IsWindows()) normalized = normalized.ToUpperInvariant();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return "StreamripperGui-" + Convert.ToHexString(hash);
    }

    public void SetActivationHandler(Action activate)
    {
        Volatile.Write(ref _activate, activate);
        if (Interlocked.Exchange(ref _pending, 0) != 0) activate();
    }

    private async Task ListenAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new System.IO.Pipes.NamedPipeServerStream(_pipeName, System.IO.Pipes.PipeDirection.InOut, 1,
                    System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                await pipe.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), timeout.Token).ConfigureAwait(false);
                var request = new byte[1];
                await pipe.ReadExactlyAsync(request, timeout.Token).ConfigureAwait(false);
                if (request[0] != 1) continue;
                Interlocked.Exchange(ref _pending, 1);
                var activate = Volatile.Read(ref _activate);
                if (activate is not null && Interlocked.Exchange(ref _pending, 0) != 0) activate();
                await pipe.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { break; }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
                System.Diagnostics.Trace.TraceWarning($"Forwarding window activation failed: {exception.Message}");
            }
        }
    }

    public static async Task<bool> ActivateExistingAsync(string directory)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", PipeName(directory), System.IO.Pipes.PipeDirection.InOut,
                System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var process = new byte[4];
            await pipe.ReadExactlyAsync(process, timeout.Token).ConfigureAwait(false);
            if (OperatingSystem.IsWindows()) AllowSetForegroundWindow(BitConverter.ToInt32(process));
            await pipe.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false);
            var response = new byte[1];
            await pipe.ReadExactlyAsync(response, timeout.Token).ConfigureAwait(false);
            return response[0] == 1;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException) { return false; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _cancellation.Cancel();
        try { _listener.GetAwaiter().GetResult(); }
        finally
        {
            _cancellation.Dispose();
            _lock.Dispose();
        }
    }
}
