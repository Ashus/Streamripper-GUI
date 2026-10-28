namespace StreamripperGui.Desktop.Views;

public sealed partial class MainWindow
{
    private double _normalWidth = 1130;
    private double _normalHeight = 840;
    private double _centerXPercent = 50;
    private double _centerYPercent = 50;
    private bool _wasMaximized;
    private bool _windowOpened;
    private int _resizeVersion;
    private double _consoleHeight;

    private void ClampConsoleHeight()
    {
        var row = WorkspaceGrid.RowDefinitions[2];
        if (!row.Height.IsAbsolute || WorkspaceGrid.Bounds.Height <= 0) return;
        var maximum = Math.Max(row.MinHeight, WorkspaceGrid.Bounds.Height - WorkspaceGrid.RowDefinitions[0].MinHeight - 6);
        var height = Math.Clamp(row.Height.Value, row.MinHeight, maximum);
        if (height != row.Height.Value) row.Height = new Avalonia.Controls.GridLength(height);
    }

    public void BringToFront()
    {
        if (WindowState == Avalonia.Controls.WindowState.Minimized)
            WindowState = _wasMaximized ? Avalonia.Controls.WindowState.Maximized : Avalonia.Controls.WindowState.Normal;
        Show();
        Activate();
        // An open modal owns input; bring it forward along with the main window.
        var dialog = OwnedWindows.LastOrDefault(window => window.IsVisible);
        dialog?.Activate();
    }

    private void RestoreWindowPreferences()
    {
        if (_windowOpened || DataContext is not ViewModels.MainWindowViewModel model) return;
        var preferences = model.ReadWindowPreferences();
        Width = _normalWidth = Math.Max(MinWidth, preferences.Width);
        Height = _normalHeight = Math.Max(MinHeight, preferences.Height);
        _centerXPercent = preferences.CenterXPercent;
        _centerYPercent = preferences.CenterYPercent;
        _wasMaximized = preferences.Maximized;
        _consoleHeight = preferences.ConsoleHeight;
        if (_consoleHeight > 0)
        {
            WorkspaceGrid.RowDefinitions[0].Height = new Avalonia.Controls.GridLength(1, Avalonia.Controls.GridUnitType.Star);
            WorkspaceGrid.RowDefinitions[2].Height = new Avalonia.Controls.GridLength(Math.Max(90, _consoleHeight));
        }
        WindowState = Avalonia.Controls.WindowState.Normal;
    }

    private void RestoreWindowPosition()
    {
        // Percentages refer to full monitor bounds; the work area only limits placement.
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
        {
            var frame = FrameSize ?? ClientSize;
            var width = frame.Width * DesktopScaling;
            var height = frame.Height * DesktopScaling;
            var bounds = screen.Bounds;
            var work = screen.WorkingArea;
            var x = bounds.X + bounds.Width * _centerXPercent / 100 - width / 2;
            var y = bounds.Y + bounds.Height * _centerYPercent / 100 - height / 2;
            Position = new Avalonia.PixelPoint(
                (int)Math.Round(Math.Clamp(x, work.X, Math.Max(work.X, work.Right - width))),
                (int)Math.Round(Math.Clamp(y, work.Y, Math.Max(work.Y, work.Bottom - height))));
        }
        _windowOpened = true;
        ClampConsoleHeight();
        _consoleHeight = ConsolePanel.Bounds.Height;
        // Establish the native normal rectangle before maximizing.
        if (_wasMaximized) WindowState = Avalonia.Controls.WindowState.Maximized;
        else QueueNormalSizeCapture();
    }

    private void QueueNormalSizeCapture()
    {
        var version = ++_resizeVersion;
        if (!_windowOpened || WindowState != Avalonia.Controls.WindowState.Normal) return;
        var size = ClientSize;
        var position = Position;
        var frame = FrameSize ?? size;
        var scaling = DesktopScaling;
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        // Native state/resize notifications can arrive in either order during maximization.
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (version != _resizeVersion || WindowState != Avalonia.Controls.WindowState.Normal) return;
            if (double.IsFinite(size.Width) && size.Width >= MinWidth &&
                double.IsFinite(size.Height) && size.Height >= MinHeight)
            {
                _normalWidth = size.Width;
                _normalHeight = size.Height;
                if (screen is { Bounds.Width: > 0, Bounds.Height: > 0 })
                {
                    _centerXPercent = Math.Clamp((position.X + frame.Width * scaling / 2 - screen.Bounds.X) / screen.Bounds.Width * 100, 0, 100);
                    _centerYPercent = Math.Clamp((position.Y + frame.Height * scaling / 2 - screen.Bounds.Y) / screen.Bounds.Height * 100, 0, 100);
                }
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
