namespace StreamripperGui.Desktop.Views;

public sealed partial class MainWindow : Avalonia.Controls.Window
{
    public MainWindow()
    {
        InitializeComponent();
        WorkspaceGrid.SizeChanged += (_, _) => ClampConsoleHeight();
        ConsolePanel.SizeChanged += (_, _) =>
        {
            if (_windowOpened && WindowState != Avalonia.Controls.WindowState.Minimized && ConsolePanel.Bounds.Height >= 90)
                _consoleHeight = ConsolePanel.Bounds.Height;
        };
        Closing += OnClosing;
        DataContextChanged += (_, _) => { BindDeletionConfirmation(); RestoreWindowPreferences(); };
        Opened += (_, _) => RestoreWindowPosition();
        Resized += (_, _) => QueueNormalSizeCapture();
        PositionChanged += (_, _) => QueueNormalSizeCapture();
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty && WindowState != Avalonia.Controls.WindowState.Minimized)
                _wasMaximized = WindowState == Avalonia.Controls.WindowState.Maximized;
        };
        Closed += (_, _) =>
        {
            if (DataContext is ViewModels.MainWindowViewModel model)
                model.SaveWindowPreferences(_normalWidth, _normalHeight, _wasMaximized, _centerXPercent, _centerYPercent, _consoleHeight);
            (DataContext as IDisposable)?.Dispose();
        };
    }

    private bool _closingAfterStop;
    private void StationFieldLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (DataContext is ViewModels.MainWindowViewModel model) model.SaveStationOnBlur();
    }
    private void OpenRelayClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (DataContext is not ViewModels.MainWindowViewModel { Recording.HasActiveRelay: true } model) return;
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(model.Recording.RelayUrl!) { UseShellExecute = true });
        }
        catch (Exception exception) { model.ReportRelayError(exception.Message); }
    }

    private void CopyRelayClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = CopyRelayAsync();

    private async Task CopyRelayAsync()
    {
        if (DataContext is not ViewModels.MainWindowViewModel { Recording.HasActiveRelay: true } model || Clipboard is null) return;
        try
        {
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(Clipboard, model.Recording.RelayUrl!);
            await Clipboard.FlushAsync();
        }
        catch (Exception exception) { model.ReportRelayError(exception.Message); }
    }

    private void OpenRecordingFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (DataContext is not ViewModels.MainWindowViewModel model || model.Recording?.Destination is not { } directory) return;
        try
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory) { UseShellExecute = true });
        }
        catch (Exception exception) { model.ReportPathError(exception); }
    }
    private bool _waitingForStop;
    private ViewModels.MainWindowViewModel? _confirmationModel;

    private void BindDeletionConfirmation()
    {
        if (_confirmationModel is not null)
        {
            _confirmationModel.ConfirmCatalogDeletion = null;
            _confirmationModel.ConfirmUnsavedChanges = null;
        }
        _confirmationModel = DataContext as ViewModels.MainWindowViewModel;
        if (_confirmationModel is not null)
        {
            _confirmationModel.ConfirmCatalogDeletion = ConfirmDeletionAsync;
            _confirmationModel.ConfirmUnsavedChanges = name => new UnsavedChangesWindow(name).ShowDialog<ViewModels.UnsavedChangesChoice>(this);
        }
    }

    private Task<bool> ConfirmDeletionAsync(ViewModels.CatalogDeletionRequest request) =>
        new DeleteConfirmationWindow(request).ShowDialog<bool>(this);

    private void OpenSettingsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = OpenSettingsAsync();

    private async Task OpenSettingsAsync()
    {
        try { await new SettingsWindow { DataContext = DataContext }.ShowDialog(this); }
        catch (Exception exception) { System.Diagnostics.Trace.TraceError($"Opening settings failed: {exception}"); }
    }

    private void OnClosing(object? sender, Avalonia.Controls.WindowClosingEventArgs args)
    {
        if (_closingAfterStop || DataContext is not ViewModels.MainWindowViewModel model || !model.HasActiveRecordings) return;
        args.Cancel = true;
        if (_waitingForStop) return;
        _waitingForStop = true;
        _ = CloseAfterStopAsync(model);
    }

    private async Task CloseAfterStopAsync(ViewModels.MainWindowViewModel model)
    {
        try
        {
            if (!await model.PrepareCloseAsync()) return;
            _closingAfterStop = true;
            Close();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Closing the recording window failed: {exception}");
        }
        finally { _waitingForStop = false; }
    }
}
