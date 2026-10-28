namespace StreamripperGui.Desktop.Views;

public sealed partial class RecordingOptionsView : Avalonia.Controls.UserControl
{
    private ViewModels.RecordingOptionsViewModel? _model;

    public RecordingOptionsView()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) => Subscribe();
        AttachedToVisualTree += (_, _) => Subscribe();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_model is not null) _model.PropertyChanged -= OptionsChanged;
            _model = null;
        };
    }

    private void Subscribe()
    {
        if (_model is not null) _model.PropertyChanged -= OptionsChanged;
        _model = DataContext as ViewModels.RecordingOptionsViewModel;
        if (_model is not null) _model.PropertyChanged += OptionsChanged;
        UpdateEmphasis();
    }

    private void OptionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => UpdateEmphasis();

    private void UpdateEmphasis()
    {
        foreach (var label in Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(this).OfType<OptionLabel>())
        {
            if (label.Tag is not string properties) continue;
            label.IsChanged = properties.Split(',').Any(p => _model?.IsNonDefault(p) == true);
        }
    }

    private void ChooseCaFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = ChooseCaFileAsync();

    private async Task ChooseCaFileAsync()
    {
        if (DataContext is not ViewModels.RecordingOptionsViewModel model || Avalonia.Controls.TopLevel.GetTopLevel(this) is not { } owner) return;
        try
        {
            var path = await PathPicker.ChooseAsync(owner, model.TlsCaFile, Localization.Localizer.Current["ChooseFile"],
                types: [new(Localization.Localizer.Current.Format("CertificateFileType")) { Patterns = ["*.pem", "*.crt", "*.cer"] }, Avalonia.Platform.Storage.FilePickerFileTypes.All]);
            if (path is not null) model.TlsCaFile = path;
        }
        catch (Exception exception)
        {
            if (owner.DataContext is ViewModels.MainWindowViewModel main) main.ReportPathError(exception);
        }
    }
}
