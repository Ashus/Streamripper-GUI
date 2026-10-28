namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _savingStation;
    public void SaveStationOnBlur()
    {
        if (_savingStation || !_initialized || !HasStationChanges || !CanSaveStation) return;
        if (_addingStation && (string.IsNullOrWhiteSpace(NameDraft) || string.IsNullOrWhiteSpace(UrlDraft))) return;
        _savingStation = true;
        try
        {
            var oldName = SelectedStation?.Name;
            SaveStation();
            if (SelectedStation is { IsActive: false } station && oldName != station.Name)
                UpdateRecordingDestination(station);
        }
        finally { _savingStation = false; }
    }

    private void UpdateRecordingDestination(StationViewModel station)
    {
        if (station.IsActive) return;
        string? destination = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(RecordingRoot))
            {
                var path = Path.Combine(Path.GetFullPath(RecordingRoot, AppContext.BaseDirectory),
                    Core.Models.StationCatalog.DirectoryName(station.Name));
                if (Directory.Exists(path)) destination = path;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Invalid settings must not prevent station selection; settings validation reports them.
        }
        station.Recording.SetDestination(destination);
    }

    private void ProfileOptionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => RefreshProfileCommands();
    partial void OnProfileNameDraftChanged(string value) { _ = value; RefreshProfileCommands(); }

    public void SaveSettingsOnBlur()
    {
        if (_initialized) SaveSettings();
    }

    private void SaveApplicationChoice(string key, string value)
    {
        if (!_initialized || _loadFailed) return;
        try
        {
            var copy = _configuration.DeepClone().AsObject();
            copy["application"]![key] = value;
            _store.SaveConfiguration(copy);
            _configuration = copy;
        }
        catch (Exception exception) { SettingsError = Message = Localization.Localizer.Current.Message("SettingsSaveFailed", exception.Message); }
    }
}
