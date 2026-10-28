namespace StreamripperGui.Desktop.Views;

public sealed partial class RulesEditorView
{
    private Avalonia.Controls.ScrollViewer? _metadataScrollViewer;
    private bool _selectingMetadata;
    private bool _updatingMetadata;

    private void InitializeMetadataScrolling()
    {
        MetadataText.AddHandler(PointerPressedEvent, MetadataPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        MetadataText.AddHandler(PointerReleasedEvent, MetadataPointerReleased, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        MetadataText.AddHandler(PointerWheelChangedEvent, MetadataUserScroll, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        MetadataText.TextArea.SelectionChanged += (_, _) =>
        {
            if (!_updatingMetadata && !MetadataText.TextArea.Selection.IsEmpty) FollowLog.IsChecked = false;
        };
        FollowLog.PropertyChanged += (_, args) =>
        {
            if (args.Property != Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty || FollowLog.IsChecked != true) return;
            MetadataText.TextArea.ClearSelection();
            UpdateLog();
            MetadataText.ScrollToEnd();
        };
        LayoutUpdated += (_, _) =>
        {
            FindMetadataScrollViewer();
            _updatingMetadata = false;
        };
        AttachedToVisualTree += (_, _) => FindMetadataScrollViewer();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_metadataScrollViewer is not null) _metadataScrollViewer.PropertyChanged -= MetadataScrollChanged;
            _metadataScrollViewer = null;
            _selectingMetadata = false;
        };
    }

    private void FindMetadataScrollViewer()
    {
        if (_metadataScrollViewer is not null) return;
        MetadataText.ApplyTemplate();
        _metadataScrollViewer = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(MetadataText)
            .OfType<Avalonia.Controls.ScrollViewer>().FirstOrDefault();
        if (_metadataScrollViewer is not null) _metadataScrollViewer.PropertyChanged += MetadataScrollChanged;
    }

    private void MetadataScrollChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != Avalonia.Controls.ScrollViewer.OffsetProperty ||
            args.OldValue is not Avalonia.Vector previous || args.NewValue is not Avalonia.Vector current ||
            Math.Abs(previous.Y - current.Y) < 0.01) return;
        SyncMetadataFollow();
    }

    private void SyncMetadataFollow(bool onlyEnable = false)
    {
        if (_metadataScrollViewer is null || _updatingMetadata || _selectingMetadata ||
            !MetadataText.TextArea.Selection.IsEmpty) return;
        var atEnd = _metadataScrollViewer.Offset.Y >= Math.Max(0,
            _metadataScrollViewer.Extent.Height - _metadataScrollViewer.Viewport.Height) - 2;
        if (!onlyEnable || atEnd) FollowLog.IsChecked = atEnd;
    }

    private void MetadataPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
    {
        _selectingMetadata = args.GetCurrentPoint(MetadataText).Properties.IsLeftButtonPressed;
        FollowLog.IsChecked = false;
    }

    private void MetadataPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs args)
    {
        _selectingMetadata = false;
        SyncMetadataFollow(true);
    }

    private void MetadataUserScroll(object? sender, Avalonia.Input.PointerWheelEventArgs args)
    {
        FollowLog.IsChecked = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SyncMetadataFollow(), Avalonia.Threading.DispatcherPriority.Background);
    }
}