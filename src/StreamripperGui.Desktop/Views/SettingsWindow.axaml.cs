namespace StreamripperGui.Desktop.Views;

public sealed partial class SettingsWindow : Avalonia.Controls.Window
{
    public SettingsWindow()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
        Closing += (_, args) =>
        {
            if (DataContext is not ViewModels.MainWindowViewModel model) return;
            model.SaveSettingsCommand.Execute(null);
            args.Cancel = model.HasSettingsError;
        };
        AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key != Avalonia.Input.Key.Escape) return;
            args.Handled = true;
            Close();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
    private void ImportClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = OpenImportAsync();
    private void CloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close();
    private void ImportRulesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = OpenRulesImportAsync();
    private async Task OpenRulesImportAsync()
    {
        if (DataContext is not ViewModels.MainWindowViewModel model) return;
        try
        {
            var directory = await PathPicker.ChooseAsync(this, model.ConfigurationDirectory, Localization.Localizer.Current["RulesImportChooseFolder"], true, null);
            if (directory is null) return;
            await new RulesImportWindow(model.CreateRulesImport(directory)).ShowDialog(this);
        }
        catch (Exception exception) { model.ReportPathError(exception); }
    }
    private void PathLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        if (DataContext is ViewModels.MainWindowViewModel model) model.SaveSettingsOnBlur();
    }

    private void ChooseRecordingFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = ChoosePathAsync(true);
    private void ChooseExecutableClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => _ = ChoosePathAsync(false);

    private async Task ChoosePathAsync(bool folder)
    {
        if (DataContext is not ViewModels.MainWindowViewModel model) return;
        try
        {
            var path = await PathPicker.ChooseAsync(this, folder ? model.RecordingRoot : model.ExecutablePath,
                Localization.Localizer.Current[folder ? "ChooseFolder" : "ChooseFile"], folder,
                folder ? null : [new("Streamripper") { Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"] }, Avalonia.Platform.Storage.FilePickerFileTypes.All]);
            if (path is null) return;
            if (folder) model.RecordingRoot = path;
            else model.ExecutablePath = path;
            model.SaveSettingsOnBlur();
        }
        catch (Exception exception) { model.ReportPathError(exception); }
    }

    private bool _openingOpml;
    private async Task OpenImportAsync()
    {
        if (_openingOpml || DataContext is not ViewModels.MainWindowViewModel { CanImportStations: true } model) return;
        _openingOpml = true;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Localization.Localizer.Current.Format("ImportStationsFromOPML"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType(Localization.Localizer.Current.Format("OpmlFileType")) { Patterns = ["*.opml", "*.xml"] },
                    Avalonia.Platform.Storage.FilePickerFileTypes.All
                ]
            });
            if (files.Count == 0) return;
            using var preview = model.CreateOpmlImport();
            using (var file = files[0])
            {
                await using var stream = await file.OpenReadAsync();
                await preview.LoadAsync(stream, file.Path.IsFile ? file.Path.LocalPath : file.Name);
            }
            if (preview.Entries.Count == 0)
            {
                model.ReportImportError(preview.Error);
                return;
            }
            await new OpmlImportWindow(preview).ShowDialog(this);
        }
        catch (Exception exception) { model.ReportImportError(exception.Message); }
        finally { _openingOpml = false; }
    }
}
