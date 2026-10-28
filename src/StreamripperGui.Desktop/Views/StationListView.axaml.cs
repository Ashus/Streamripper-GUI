namespace StreamripperGui.Desktop.Views;

public sealed partial class StationListView : Avalonia.Controls.UserControl
{
    private ViewModels.MainWindowViewModel? _model;
    private Avalonia.Controls.ContextMenu? _stationMenu;

    public StationListView()
    {
        InitializeComponent();
        AddHandler(ContextRequestedEvent, StationContextRequested, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        SizeChanged += (_, _) => PinnedList.MaxHeight = Math.Min(220, Bounds.Height * 0.4);
        DataContextChanged += (_, _) => Subscribe();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_model is not null) _model.StationPinned -= Pinned;
            _model = null;
            _stationMenu?.Close();
            _stationMenu = null;
        };
        AttachedToVisualTree += (_, _) => Subscribe();
    }

    private void Subscribe()
    {
        if (_model is not null) _model.StationPinned -= Pinned;
        _model = DataContext as ViewModels.MainWindowViewModel;
        if (_model is not null) _model.StationPinned += Pinned;
    }

    private void Pinned(ViewModels.StationViewModel station) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => PinnedList.ScrollIntoView(station));

    private void StationPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
    {
        if (sender is Avalonia.Controls.Control { DataContext: ViewModels.StationViewModel station } && _model is not null)
            _model.SelectedStation = station;
    }

    private void StationDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs args)
    {
        if (sender is Avalonia.Controls.Control { DataContext: ViewModels.StationViewModel station } && _model is not null)
        {
            args.Handled = true;
            _ = ToggleAsync(station);
        }
    }

    private async Task ToggleAsync(ViewModels.StationViewModel station)
    {
        try { if (_model is not null) await _model.ToggleStationAsync(station); }
        catch (Exception exception) { System.Diagnostics.Trace.TraceError($"Toggling recording failed: {exception}"); }
    }

    private void RemoveStationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (sender is Avalonia.Controls.Control { DataContext: ViewModels.StationViewModel station })
            _model?.RemoveStationFromList(station);
    }

    private void StationContextRequested(object? sender, Avalonia.Input.ContextRequestedEventArgs args)
    {
        var source = args.Source as Avalonia.Controls.Control;
        var container = source is Avalonia.Controls.ListBoxItem item ? item : source is null ? null :
            Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(source).OfType<Avalonia.Controls.ListBoxItem>().FirstOrDefault();
        var station = container?.DataContext as ViewModels.StationViewModel;
        if (station is null && !args.TryGetPosition(this, out _) && source is Avalonia.Controls.ListBox list)
            station = list.SelectedItem as ViewModels.StationViewModel;
        if (station is null || _model is null) return;
        _model.SelectedStation = station;
        var localizer = Localization.Localizer.Current;
        _stationMenu?.Close();
        var menu = new Avalonia.Controls.ContextMenu
        {
            ItemsSource = new Avalonia.Controls.MenuItem[]
            {
                new() { Header = Localization.Localizer.Current.Format("Start"), Command = station.Recording.StartCommand },
                new() { Header = Localization.Localizer.Current.Format("Stop"), Command = station.Recording.StopCommand },
                new() { Header = Localization.Localizer.Current.Format("RemoveStation"), IsEnabled = station.Recording.CanEdit, DataContext = station }
            }
        };
        ((Avalonia.Controls.MenuItem[])menu.ItemsSource)[2].Click += RemoveStationClick;
        _stationMenu = menu;
        args.Handled = true;
        menu.Open(container ?? source!);
    }
}
