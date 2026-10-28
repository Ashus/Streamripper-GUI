namespace StreamripperGui.Desktop.ViewModels;

/// <summary>Owns one station's recording lifecycle, independently of the selected station.</summary>
public sealed partial class RecordingViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private readonly ConsoleViewModel _console;
    private Services.IRecordingProcess? _process;
    private Task? _completion;
    private bool _stopRequested;
    private Core.Models.StreamSummaryParser? _summary;
    private int _generation;
    private volatile bool _unsupportedRules;
    internal string? MetadataRulesPath { get; set; }
    private string? _activeRulesPath;
    public bool UsesRulesFile(string path) => IsActive && !_unsupportedRules && _activeRulesPath is not null &&
        string.Equals(_activeRulesPath, path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(HasDestination))]
    public partial string? Destination { get; private set; }
    public bool HasDestination => Destination is not null;
    internal void SetDestination(string? path) { if (!IsActive) Destination = path; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(HasActiveRelay))]
    public partial string? RelayUrl { get; private set; }
    public bool HasActiveRelay => IsActive && RelayUrl is not null;

    public RecordingViewModel(ConsoleViewModel console)
    {
        _console = console;
    }

    internal Action? PrepareStart { get; set; }
    internal Action<Core.Models.StreamObservation>? StreamObserved { get; set; }
    internal Core.Models.RecordingOptions Options { get; set; } = new();

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(CanEdit))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(HasActiveRelay))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedFor(nameof(ForceStopCommand))]
    public partial bool IsActive { get; private set; }

    public bool CanEdit => !IsActive;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyCanExecuteChangedFor(nameof(ForceStopCommand))]
    public partial bool CanForceStop { get; private set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string ExecutablePath { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string RecordingRoot { get; set; } = Infrastructure.PlatformPaths.RecordingDirectory;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Url { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string StationName { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Status { get; private set; } = Localization.Localizer.Current.Message("Ready");

    private bool CanStart() => !IsActive;
    private bool CanStop() => IsActive;
    private bool CanForce() => IsActive && CanForceStop;

    public void RefreshCommands() => StartCommand.NotifyCanExecuteChanged();

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        try
        {
            RelayUrl = null;
            PrepareStart?.Invoke();
            if (!Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException(Localization.Localizer.Current.Message("StationUrlInvalid"));
            if (string.IsNullOrWhiteSpace(ExecutablePath)) throw new ArgumentException(Localization.Localizer.Current.Message("StreamripperPathRequired"));
            var executable = Path.GetFullPath(ExecutablePath.Trim(), AppContext.BaseDirectory);
            if (!File.Exists(executable)) throw new FileNotFoundException(Localization.Localizer.Current.Message("StreamripperNotFound"), executable);
            if (string.IsNullOrWhiteSpace(RecordingRoot)) throw new ArgumentException(Localization.Localizer.Current.Message("RecordingDirectoryRequired"));
            var name = Core.Models.StationCatalog.DirectoryName(StationName);
            var destination = Path.Combine(Path.GetFullPath(RecordingRoot.Trim(), AppContext.BaseDirectory), name);
            Destination = destination;
            Directory.CreateDirectory(destination);
            var options = Options with { };
            var arguments = Core.Models.RecordingArguments.Build(uri.AbsoluteUri, destination, options, MetadataRulesPath);
            _unsupportedRules = false;
            var generation = ++_generation;
            _summary = new Core.Models.StreamSummaryParser(Url);
            _summary.Observed += observation => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_generation != generation) return;
                RelayUrl = IsActive ? Core.Models.RelayEndpoint.ToUrl(observation.RelayAddress, observation.RelayPort) : null;
                StreamObserved?.Invoke(observation);
            });
            _console.BeginRecording();
            _process = Services.RecordingProcess.Start(executable, arguments);
            _activeRulesPath = MetadataRulesPath;
            _stopRequested = false;
            CanForceStop = false;
            IsActive = true;
            Status = Localization.Localizer.Current.Message("Recording");
            _completion = MonitorAsync(_process, executable, uri.AbsoluteUri, destination, options, MetadataRulesPath is not null);
        }
        catch (Exception exception)
        {
            Status = Localization.Localizer.Current.Message("RecordingStartFailed", exception.Message);
            _console.ReportError(Status);
            if (!IsActive) _console.EndRecording(Status);
        }
    }

    private async Task MonitorAsync(Services.IRecordingProcess process, string executable, string url, string destination, Core.Models.RecordingOptions options, bool usedRules)
    {
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(),
                PumpAsync(process.StandardOutput, Core.Console.ConsoleChannel.StandardOutput),
                PumpAsync(process.StandardError, Core.Console.ConsoleChannel.StandardError));
            if (usedRules && _unsupportedRules && !_stopRequested)
            {
                _console.ReportError(Localization.Localizer.Current.Message("RulesUnsupportedRestart"));
                var next = Services.RecordingProcess.Start(executable, Core.Models.RecordingArguments.Build(url, destination, options));
                process.Dispose();
                process = next;
                _process = process;
                await Task.WhenAll(process.WaitForExitAsync(),
                    PumpAsync(process.StandardOutput, Core.Console.ConsoleChannel.StandardOutput),
                    PumpAsync(process.StandardError, Core.Console.ConsoleChannel.StandardError));
            }
            Status = _stopRequested
                ? Localization.Localizer.Current.Message("RecordingStopped")
                : Localization.Localizer.Current.Message("RecordingProcessExited", process.ExitCode);
        }
        catch (Exception exception)
        {
            Status = Localization.Localizer.Current.Message("RecordingFailed", exception.Message);
            _console.ReportError(Status);
            // Do not abandon a live writer when pipe reading fails.
            if (!process.HasExited)
            {
                CanForceStop = true;
                await process.WaitForExitAsync();
            }
        }
        finally
        {
            process.Dispose();
            _process = null;
            IsActive = false;
            RelayUrl = null;
            CanForceStop = false;
            _activeRulesPath = null;
            _console.EndRecording(Status);
        }
    }

    private async Task PumpAsync(Stream stream, Core.Console.ConsoleChannel channel)
    {
        var bytes = new byte[8192];
        var diagnostics = "";
        int count;
        while ((count = await stream.ReadAsync(bytes).ConfigureAwait(false)) != 0)
        {
            _console.Append(channel, bytes.AsSpan(0, count));
            if (channel == Core.Console.ConsoleChannel.StandardError)
            {
                diagnostics += System.Text.Encoding.UTF8.GetString(bytes, 0, count);
                if (diagnostics.Contains("--metadata-rules is unavailable for whole-stream HLS/FLAC recording", StringComparison.Ordinal))
                    _unsupportedRules = true;
                if (diagnostics.Length > 256) diagnostics = diagnostics[^256..];
            }
            if (channel == Core.Console.ConsoleChannel.StandardOutput) _summary?.Append(bytes.AsSpan(0, count));
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        var process = _process;
        var completion = _completion;
        if (process is null || completion is null) return;
        _stopRequested = true;
        Status = Localization.Localizer.Current.Message("RecordingStopping");
        try
        {
            await process.RequestStopAsync();
            await completion.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            Status = Localization.Localizer.Current.Message("RecordingStopTimeout");
            CanForceStop = IsActive;
        }
        catch (Exception exception)
        {
            if (!IsActive) return;
            Status = Localization.Localizer.Current.Message("RecordingStopFailed", exception.Message);
            CanForceStop = true;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanForce))]
    private void ForceStop()
    {
        try
        {
            _stopRequested = true;
            _process?.ForceStop();
        }
        catch (Exception exception)
        {
            Status = Localization.Localizer.Current.Message("RecordingForceStopFailed", exception.Message);
        }
    }

    public async Task<bool> PrepareCloseAsync()
    {
        if (!IsActive) return true;
        if (StopCommand is { IsRunning: true, ExecutionTask: { } stopping })
            await stopping;
        else
            await StopCommand.ExecuteAsync(null);
        return !IsActive;
    }
}
