namespace StreamripperGui.Desktop.Views;

public sealed partial class ConsoleView : Avalonia.Controls.UserControl
{
    private ViewModels.ConsoleViewModel? _viewModel;
    private Core.Console.ConsoleTextProjection _projection = new();
    private Avalonia.Controls.ScrollViewer? _scrollViewer;
    private bool _scrollingToEnd;
    private bool _selectingText;
    private bool _updatingTranscript;

    public ConsoleView()
    {
        InitializeComponent();
        Transcript.Document.UndoStack.SizeLimit = 0;
        Transcript.Options.EnableHyperlinks = false;
        Transcript.Options.EnableEmailHyperlinks = false;
        Transcript.Options.AllowScrollBelowDocument = false;
        Transcript.TextArea.SelectionForeground = Avalonia.Media.Brushes.White;
        // Apply diagnostic colors before the editor's built-in selection foreground.
        Transcript.TextArea.TextView.LineTransformers.Insert(0, new DiagnosticColorizer(this));
        Transcript.TextArea.SelectionChanged += SelectionChanged;
        AttachedToVisualTree += Attached;
        DetachedFromVisualTree += Detached;
        DataContextChanged += ContextChanged;
        PropertyChanged += ThemeChanged;
        LayoutUpdated += LayoutChanged;
        Transcript.AddHandler(PointerPressedEvent, TextPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Transcript.AddHandler(PointerReleasedEvent, TextPointerReleased,
            Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        Transcript.AddHandler(KeyDownEvent, TextKeyDown,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Transcript.AddHandler(PointerWheelChangedEvent, UserScroll,
            Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
    }

    private void Attached(object? sender, Avalonia.VisualTreeAttachmentEventArgs args)
    {
        FindScrollViewer();
        Subscribe();
    }

    private void Detached(object? sender, Avalonia.VisualTreeAttachmentEventArgs args)
    {
        Unsubscribe();
        if (_scrollViewer is not null) _scrollViewer.PropertyChanged -= ScrollPropertyChanged;
        _scrollViewer = null;
    }

    private void FindScrollViewer()
    {
        if (_scrollViewer is not null) return;
        Transcript.ApplyTemplate();
        _scrollViewer = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(Transcript)
            .OfType<Avalonia.Controls.ScrollViewer>().FirstOrDefault();
        if (_scrollViewer is not null) _scrollViewer.PropertyChanged += ScrollPropertyChanged;
    }

    private void ContextChanged(object? sender, EventArgs args)
    {
        Unsubscribe();
        if (Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(this)) Subscribe();
    }

    private void Subscribe()
    {
        Unsubscribe();
        _viewModel = DataContext as ViewModels.ConsoleViewModel;
        _projection = new Core.Console.ConsoleTextProjection();
        _updatingTranscript = true;
        Transcript.Document.Text = "";
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += ViewModelChanged;
        _viewModel.UpdatingLines += UpdatingLines;
        _viewModel.LinesUpdated += LinesUpdated;
        LinesUpdated(this, EventArgs.Empty);
    }

    private void Unsubscribe()
    {
        ClearTextSelection();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModelChanged;
            _viewModel.UpdatingLines -= UpdatingLines;
            _viewModel.LinesUpdated -= LinesUpdated;
        }
        _viewModel = null;
        _updatingTranscript = false;
    }

    private void UpdatingLines(object? sender, EventArgs args)
    {
        // History eviction and document edits can move the scroll anchor.
        _updatingTranscript = true;
        InvalidateArrange();
    }

    private void LinesUpdated(object? sender, EventArgs args)
    {
        if (_viewModel is null) return;
        using (Transcript.Document.RunUpdate())
        {
            foreach (var edit in _projection.Update(_viewModel.Lines))
                Transcript.Document.Replace(edit.Offset, edit.Length, edit.Text);
        }
    }

    private void ScrollPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs args)
    {
        if (_viewModel is null || _scrollViewer is null || _scrollingToEnd || _updatingTranscript ||
            args.Property != Avalonia.Controls.ScrollViewer.OffsetProperty ||
            args.OldValue is not Avalonia.Vector previous || args.NewValue is not Avalonia.Vector current ||
            Math.Abs(previous.Y - current.Y) < 0.01) return;
        SyncFollowWithPosition();
    }

    private void ViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewModels.ConsoleViewModel.FollowOutput)) return;
        if (_viewModel?.FollowOutput == true) ClearTextSelection();
        FollowToEnd();
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SyncFollowWithPosition(true), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UserScroll(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        _viewModel?.FollowOutput = false;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => SyncFollowWithPosition(), Avalonia.Threading.DispatcherPriority.Background);
    }

    private void SyncFollowWithPosition(bool onlyEnable = false)
    {
        if (_viewModel is null || _scrollViewer is null || _updatingTranscript || _scrollingToEnd ||
            _selectingText || !Transcript.TextArea.Selection.IsEmpty) return;
        var atEnd = _scrollViewer.Offset.Y >= Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height) - 2;
        if (!onlyEnable || atEnd) _viewModel.FollowOutput = atEnd;
    }

    private void LayoutChanged(object? sender, EventArgs args)
    {
        FindScrollViewer();
        FollowToEnd();
        _updatingTranscript = false;
        SyncFollowWithPosition(true);
    }

    private void FollowToEnd()
    {
        if (_viewModel?.FollowOutput != true || _viewModel.IsDisplayPaused || _scrollingToEnd || _scrollViewer is null) return;
        var end = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        // The editor rounds its viewport to pixels; tolerate the subpixel difference.
        if (Math.Abs(_scrollViewer.Offset.Y - end) < 1) return;
        // Follow before rendering rather than displaying an intermediate old offset.
        _scrollingToEnd = true;
        try { _scrollViewer.Offset = new Avalonia.Vector(_scrollViewer.Offset.X, end); }
        finally { _scrollingToEnd = false; }
    }

    private void TextPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
    {
        _selectingText = args.GetCurrentPoint(Transcript).Properties.IsLeftButtonPressed;
        _viewModel?.FollowOutput = false;
        UpdateSelectionPause();
    }

    private void TextPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs args)
    {
        _selectingText = false;
        UpdateSelectionPause();
        SyncFollowWithPosition(true);
    }

    private void TextKeyDown(object? sender, Avalonia.Input.KeyEventArgs args)
    {
        var copyModifier = (args.KeyModifiers &
            (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Meta)) != 0;
        switch (args.Key)
        {
            case Avalonia.Input.Key.C when copyModifier:
                args.Handled = true;
                _ = CopyAsync();
                return;
            case Avalonia.Input.Key.A when copyModifier:
                args.Handled = true;
                Transcript.SelectAll();
                return;
        }
        if (args.Key != Avalonia.Input.Key.Escape) return;
        ClearTextSelection();
        args.Handled = true;
    }

    private void SelectionChanged(object? sender, EventArgs args)
    {
        if (_updatingTranscript) return;
        if (!Transcript.TextArea.Selection.IsEmpty) _viewModel?.FollowOutput = false;
        UpdateSelectionPause();
    }

    private void UpdateSelectionPause() =>
        _viewModel?.IsDisplayPaused = _selectingText || !Transcript.TextArea.Selection.IsEmpty;

    private void ClearTextSelection()
    {
        _selectingText = false;
        Transcript.TextArea.ClearSelection();
        UpdateSelectionPause();
        SyncFollowWithPosition(true);
    }

    private void ThemeChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property.Name == nameof(ActualThemeVariant)) Transcript.TextArea.TextView.Redraw();
    }

    private void CopySelectedClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
        _ = CopyAsync();

    private async Task CopyAsync()
    {
        try
        {
            var text = Transcript.SelectedText;
            if (string.IsNullOrEmpty(text)) return;
            var clipboard = Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text);
            await clipboard.FlushAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Console clipboard copy failed: {0}", exception.Message);
        }
    }

    private sealed class DiagnosticColorizer(ConsoleView owner) : AvaloniaEdit.Rendering.DocumentColorizingTransformer
    {
        private static readonly Avalonia.Media.IBrush LightError = Avalonia.Media.Brush.Parse("#B42318");
        private static readonly Avalonia.Media.IBrush DarkError = Avalonia.Media.Brush.Parse("#FF9B9B");

        protected override void ColorizeLine(AvaloniaEdit.Document.DocumentLine line)
        {
            var rows = owner._viewModel?.Lines;
            if (rows is null || line.LineNumber > rows.Count ||
                rows[line.LineNumber - 1].Channel != Core.Console.ConsoleChannel.StandardError) return;
            var brush = owner.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? DarkError : LightError;
            ChangeLinePart(line.Offset, line.EndOffset, element => element.TextRunProperties.SetForegroundBrush(brush));
        }
    }
}
