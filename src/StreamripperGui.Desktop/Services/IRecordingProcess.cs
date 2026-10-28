namespace StreamripperGui.Desktop.Services;

internal interface IRecordingProcess : IDisposable
{
    Stream StandardOutput { get; }
    Stream StandardError { get; }
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    Task WaitForExitAsync();
    Task RequestStopAsync();
    void ForceStop();
}