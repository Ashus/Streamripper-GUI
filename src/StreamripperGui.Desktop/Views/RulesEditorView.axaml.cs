namespace StreamripperGui.Desktop.Views;

public sealed partial class RulesEditorView : Avalonia.Controls.UserControl
{
    private ViewModels.MainWindowViewModel? _main;
    private ViewModels.RulesEditorViewModel? _model;
    private bool _updating;
    private bool _clearingLog;
    private readonly Avalonia.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };

    public RulesEditorView()
    {
        InitializeComponent();
        RuleText.AddHandler(KeyDownEvent, EditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RuleText.AddHandler(PointerPressedEvent, RulePointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        MetadataText.AddHandler(KeyDownEvent, EditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        RuleText.Options.AllowScrollBelowDocument = false;
        MetadataText.Options.AllowScrollBelowDocument = false;
        MetadataText.Document.UndoStack.SizeLimit = 0;
        RuleText.TextArea.SelectionForeground = Avalonia.Media.Brushes.White;
        MetadataText.TextArea.SelectionForeground = Avalonia.Media.Brushes.White;
        RuleText.TextArea.TextView.LineTransformers.Insert(0, new RuleColorizer(this));
        PropertyChanged += (_, args) =>
        {
            if (args.Property.Name == nameof(ActualThemeVariant)) RuleText.TextArea.TextView.Redraw();
        };
        RuleText.TextChanged += (_, _) => { if (!_updating && _model is not null) _model.Text = RuleText.Text; };
        RuleText.TextArea.Caret.PositionChanged += (_, _) => { if (_model is not null) _model.CaretOffset = RuleText.CaretOffset; };
        InitializeMetadataScrolling();
        _timer.Tick += (_, _) => UpdateLog();
        AttachedToVisualTree += (_, _) => { Subscribe(); _timer.Start(); };
        DetachedFromVisualTree += (_, _) => { _timer.Stop(); Unsubscribe(); };
        DataContextChanged += (_, _) => Subscribe();
    }

    private void RulePointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(RuleText).Properties.IsRightButtonPressed) return;
        var position = RuleText.GetPositionFromPoint(args.GetPosition(RuleText));
        if (position is null) return;
        var offset = RuleText.Document.GetOffset(position.Value.Location);
        RuleText.Focus();
        if (!RuleText.TextArea.Selection.Contains(offset)) RuleText.TextArea.ClearSelection();
        RuleText.CaretOffset = offset;
    }

    private void RuleMenuOpening(object? sender, System.ComponentModel.CancelEventArgs args)
    {
        CopyRule.IsEnabled = !RuleText.TextArea.Selection.IsEmpty;
        CutRule.IsEnabled = CopyRule.IsEnabled && !RuleText.IsReadOnly;
        PasteRule.IsEnabled = !RuleText.IsReadOnly;
    }

    private void CutRuleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => RuleText.Cut();
    private void CopyRuleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => RuleText.Copy();
    private void PasteRuleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => RuleText.Paste();

    private static void EditorKeyDown(object? sender, Avalonia.Input.KeyEventArgs args)
    {
        if (sender is not AvaloniaEdit.TextEditor editor) return;
        if (args.Key == Avalonia.Input.Key.Insert && args.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
            editor.Copy();
        else if (args.Key == Avalonia.Input.Key.Delete && args.KeyModifiers == Avalonia.Input.KeyModifiers.Shift && !editor.IsReadOnly)
            editor.Cut();
        else if (args.Key == Avalonia.Input.Key.Insert && args.KeyModifiers == Avalonia.Input.KeyModifiers.Shift && !editor.IsReadOnly)
            editor.Paste();
        else return;
        args.Handled = true;
    }

    private void Subscribe()
    {
        Unsubscribe();
        _main = DataContext as ViewModels.MainWindowViewModel;
        _model = _main?.Rules;
        if (_model is null) { ClearMetadataLog.IsEnabled = false; return; }
        _model.PropertyChanged += ModelChanged;
        _model.DiagnosticsChanged += DiagnosticsChanged;
        _main!.PropertyChanged += MainChanged;
        SyncText();
        UpdateLog();
    }

    private void Unsubscribe()
    {
        if (_model is not null)
        {
            _model.PropertyChanged -= ModelChanged;
            _model.DiagnosticsChanged -= DiagnosticsChanged;
        }
        if (_main is not null) _main.PropertyChanged -= MainChanged;
        _model = null;
        _main = null;
    }

    private void MainChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ViewModels.MainWindowViewModel.SelectedStation)) return;
        _updatingMetadata = true;
        MetadataText.Text = "";
        FollowLog.IsChecked = true;
        UpdateLog();
    }

    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewModels.RulesEditorViewModel.Text)) SyncText();
        if (args.PropertyName == nameof(ViewModels.RulesEditorViewModel.CaretOffset) && _model is not null)
            RuleText.CaretOffset = Math.Clamp(_model.CaretOffset, 0, RuleText.Document.TextLength);
    }

    private void SyncText()
    {
        if (_model is null || RuleText.Text == _model.Text) return;
        _updating = true;
        try { RuleText.Text = _model.Text; }
        finally { _updating = false; }
    }

    private void DiagnosticsChanged(object? sender, EventArgs args) => RuleText.TextArea.TextView.Redraw();

    private void UpdateLog()
    {
        try
        {
            var path = MetadataLogPath();
            var exists = path is not null && File.Exists(path);
            ClearMetadataLog.IsEnabled = exists && !_clearingLog;
            if (_clearingLog || FollowLog.IsChecked != true || !MetadataText.TextArea.Selection.IsEmpty) return;
            LogStatus.Text = Localization.Localizer.Current["SelectedStationMetadataLog"];
            if (!exists) { _updatingMetadata = true; MetadataText.Text = ""; LogStatus.Text = Localization.Localizer.Current.Format("MetadataLogMissing"); return; }
            var text = Infrastructure.MetadataLogTail.Read(path!);
            if (MetadataText.Text == text) return;
            _updatingMetadata = true;
            MetadataText.Text = text;
            MetadataText.ScrollToEnd();
        }
        catch (Exception exception) { ClearMetadataLog.IsEnabled = false; LogStatus.Text = Localization.Localizer.Current.Format("LogRead", exception.Message); }
    }

    private string? MetadataLogPath()
    {
        if (_main?.SelectedStation is not { } station) return null;
        var destination = station.Recording.Destination ?? Path.Combine(
            Path.GetFullPath(_main.RecordingRoot, AppContext.BaseDirectory), Core.Models.StationCatalog.DirectoryName(station.Name));
        return Path.Combine(destination, "incomplete", "metadata.log");
    }

    private async void ClearMetadataClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (_clearingLog) return;
        var path = MetadataLogPath();
        if (path is null || !File.Exists(path)) { UpdateLog(); return; }
        _clearingLog = true;
        ClearMetadataLog.IsEnabled = false;
        try
        {
            await Infrastructure.MetadataLogTail.ClearAsync(path);
            // A retry may span a station switch; never clear another station's displayed log.
            if (path == MetadataLogPath())
            {
                _updatingMetadata = true;
                MetadataText.Text = "";
            }
        }
        catch (Exception exception)
        {
            LogStatus.Text = Localization.Localizer.Current.Format("ClearMetadataLogFailed", exception.Message);
        }
        finally
        {
            _clearingLog = false;
            ClearMetadataLog.IsEnabled = MetadataLogPath() is { } current && File.Exists(current);
        }
    }

    private void UseMetadataClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (_model is null || MetadataText.Document.TextLength == 0) return;
        var line = MetadataText.Document.GetLineByOffset(MetadataText.CaretOffset);
        _model.LiteralText = Infrastructure.MetadataLogTail.DecodeLine(MetadataText.Document.GetText(line.Offset, line.Length));
    }

    private sealed class RuleColorizer(RulesEditorView owner) : AvaloniaEdit.Rendering.DocumentColorizingTransformer
    {
        protected override void ColorizeLine(AvaloniaEdit.Document.DocumentLine line)
        {
            var value = CurrentContext.Document.GetText(line.Offset, line.Length);
            var dark = owner.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
            if (owner._model?.Diagnostics.Any(d => d.Line == line.LineNumber && !d.IsWarning) == true)
            {
                ChangeLinePart(line.Offset, line.EndOffset, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#FF9B9B" : "#B42318")));
                return;
            }
            if (value.TrimStart().StartsWith('#'))
            {
                ChangeLinePart(line.Offset, line.EndOffset, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#8FC98F" : "#397039")));
                return;
            }
            var rule = Core.Models.MetadataRules.Parse(value).Rules.FirstOrDefault();
            if (rule is null) return;
            var start = line.Offset + rule.PatternColumn - 1;
            ChangeLinePart(start, start + rule.PatternLength, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#9CDCFE" : "#185A87")));
            var command = line.Offset + value.Length - value.TrimStart().Length;
            ChangeLinePart(command, command + 2, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#DCDCAA" : "#795E26")));
            for (var index = 0; index < rule.Pattern.Length; index++)
            {
                if (!"\\^$.*+?()[]{}|".Contains(rule.Pattern[index])) continue;
                var length = rule.Pattern[index] == '\\' && index + 1 < rule.Pattern.Length ? 2 : 1;
                ChangeLinePart(start + index, start + index + length, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#C586C0" : "#8250A0")));
                index += length - 1;
            }
            if (rule.Command == 's' && rule.Replacement.Length > 0)
            {
                var replacement = line.Offset + rule.ReplacementColumn - 1;
                ChangeLinePart(replacement, replacement + rule.Replacement.Length, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#CE9178" : "#A04D27")));
            }
            var flags = line.Offset + rule.FlagsColumn - 1;
            if (flags < line.EndOffset)
                ChangeLinePart(flags, line.EndOffset, e => e.TextRunProperties.SetForegroundBrush(Avalonia.Media.Brush.Parse(dark ? "#D6A8ED" : "#7A3D91")));
        }
    }
}
