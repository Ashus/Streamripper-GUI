namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class ConsoleViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private readonly Core.Console.ConsoleBuffer _buffer = new();
    private readonly Avalonia.Threading.DispatcherTimer _timer;
    private long _displayedRevision = -1;
    private bool _disposed;
    private bool _recording;

    public ConsoleViewModel()
    {
        ClearCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(Clear, () => !_recording);
        _timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(34) };
        _timer.Tick += Tick;
        _timer.Start();
    }

    public IReadOnlyList<Core.Console.ConsoleLine> Lines { get; private set; } = [];
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Title { get; set; } = Localization.Localizer.Current.Message("Console");
    public CommunityToolkit.Mvvm.Input.RelayCommand ClearCommand { get; }
    public event EventHandler? UpdatingLines;
    public event EventHandler? LinesUpdated;

    internal void BeginRecording()
    {
        Clear();
        _recording = true;
        FollowOutput = true;
        Status = Localization.Localizer.Current.Message("StreamripperRunning");
        ClearCommand.NotifyCanExecuteChanged();
    }

    internal void Append(Core.Console.ConsoleChannel channel, ReadOnlySpan<byte> bytes) => _buffer.Append(channel, bytes);

    internal void ReportError(string message) =>
        Append(Core.Console.ConsoleChannel.StandardError, System.Text.Encoding.UTF8.GetBytes(message + "\n"));

    internal void EndRecording(string status)
    {
        _buffer.Complete(Core.Console.ConsoleChannel.StandardOutput);
        _buffer.Complete(Core.Console.ConsoleChannel.StandardError);
        _recording = false;
        Status = status;
        ClearCommand.NotifyCanExecuteChanged();
        Refresh();
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Status { get; private set; } = Localization.Localizer.Current.Message("Ready");

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool FollowOutput { get; set; } = true;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsDisplayPaused { get; set; }

    private void Clear()
    {
        IsDisplayPaused = false;
        _buffer.Clear();
        Refresh();
        Status = Localization.Localizer.Current.Message("Ready");
    }

    private void Tick(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        if (IsDisplayPaused) return;
        var revision = _buffer.Revision;
        if (revision == _displayedRevision) return;
        UpdatingLines?.Invoke(this, EventArgs.Empty);
        Lines = _buffer.Snapshot();
        _displayedRevision = revision;
        LinesUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= Tick;
    }
}
