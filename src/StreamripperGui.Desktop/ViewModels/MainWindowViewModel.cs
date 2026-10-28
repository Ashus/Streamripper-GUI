namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private readonly Infrastructure.JsonConfigurationStore _store;
    private readonly List<StationViewModel> _stations = [];
    private Infrastructure.StationFile _stationFile = new();
    private System.Text.Json.Nodes.JsonObject _configuration = Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration();
    private readonly bool _loadFailed;
    private bool _refreshingList;
    private bool _addingStation;
    private bool _initialized;
    private bool _savingSettings;
    private readonly ConsoleViewModel _emptyConsole = new();
    private StationViewModel? _selectedStation;

    public MainWindowViewModel() : this(new Infrastructure.JsonConfigurationStore(
        Infrastructure.JsonConfigurationStore.DefaultDirectory(AppContext.BaseDirectory))) { }

    public MainWindowViewModel(Infrastructure.JsonConfigurationStore store)
    {
        _store = store;
        try
        {
            _configuration = _store.LoadConfiguration();
            _stationFile = _store.LoadStations(_configuration);
            var application = _configuration["application"]!;
            ExecutablePath = application["streamripperPath"]?.GetValue<string>() ?? Infrastructure.PlatformPaths.StreamripperExecutable;
#if DEBUG
            // The prescribed development executable is outside the portable application layout.
            if (OperatingSystem.IsWindows() && !File.Exists(Path.GetFullPath(ExecutablePath, AppContext.BaseDirectory)))
                ExecutablePath = @"C:\Users\Ashus\SparkleShare\appdata\StreamRipper\streamripper\streamripper.exe";
#endif
            RecordingRoot = _configuration["recording"]!["directory"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(RecordingRoot))
                RecordingRoot = _store.IsPortable ? "recordings" : Infrastructure.PlatformPaths.RecordingDirectory;
            LoadProfiles();
            LoadRules();
            InitializeLocalization();
            InitializeAppearance();
            foreach (var station in _stationFile.Stations) Attach(station);
            Message = Localization.Localizer.Current.Message("StationsLoaded", _stations.Count);
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            Message = Localization.Localizer.Current.Message("ConfigLoad", exception.Message);
        }
        RefreshStations();
        SelectedStation = VisibleStations.FirstOrDefault();
        _initialized = true;
        DiscoverRules();
    }

    public System.Collections.ObjectModel.ObservableCollection<StationViewModel> VisibleStations { get; } = [];
    public ConsoleViewModel Console => SelectedStation?.Console ?? _emptyConsole;
    public RecordingViewModel? Recording => SelectedStation?.Recording;
    public bool HasSelection => SelectedStation is not null;
    public StationViewModel? ListSelection
    {
        get => VisibleStations.Contains(SelectedStation!) ? SelectedStation : null;
        set { if (!_refreshingList && value is not null) SelectedStation = value; }
    }
    public bool HasActiveRecordings => _stations.Any(s => s.IsActive);
    public string ConfigurationDirectory => _store.DirectoryPath;
    public bool CanSaveStation => !_loadFailed && (_addingStation || SelectedStation is { IsActive: false });
    public bool HasStationChanges => CanSaveStation && (_addingStation || SelectedStation is { } station &&
        (NameDraft != station.Name || UrlDraft != station.Url ||
         StationProfileDraft?.Id != station.Data.RecordingProfileId || StationRuleDraft?.Id != station.Data.MetadataRuleSetId));
    public bool CanRemoveStation => !_loadFailed && !_addingStation && SelectedStation is { IsActive: false };
    public string EditorTitle => _addingStation ? Localization.Localizer.Current.Message("NewStation") : Localization.Localizer.Current.Message("StationConfiguration");

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value) => RefreshStations(value);

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string NameDraft { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string UrlDraft { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string ExecutablePath { get; set; } = Infrastructure.PlatformPaths.StreamripperExecutable;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string RecordingRoot { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Message { get; private set; } = Localization.Localizer.Current.Message("Ready");

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(HasSettingsError))]
    public partial string SettingsError { get; private set; } = "";
    public bool HasSettingsError => SettingsError.Length != 0;

    public StationViewModel? SelectedStation
    {
        get => _selectedStation;
        set
        {
            // List refresh/filtering must not discard the currently displayed station.
            if (_refreshingList) return;
            if (_initialized && !_acceptStationSelection && value != _selectedStation && SelectedTabIndex is 1 or 2)
            {
                _ = SelectStationAsync(value);
                return;
            }
            if (_initialized && !_savingStation && value != _selectedStation) SaveStationOnBlur();
            if (!SetProperty(ref _selectedStation, value)) return;
            _addingStation = false;
            NameDraft = value?.Name ?? "";
            UrlDraft = value?.Url ?? "";
            StationProfileDraft = ProfileChoices.FirstOrDefault(p => p.Id == value?.Data.RecordingProfileId);
            StationRuleDraft = RuleChoices.FirstOrDefault(r => r.Id == value?.Data.MetadataRuleSetId);
            if (value is not null) UpdateRecordingDestination(value);
            OnPropertyChanged(nameof(Console));
            OnPropertyChanged(nameof(Recording));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(ListSelection));
            OnPropertyChanged(nameof(PinnedSelection));
            OnPropertyChanged(nameof(OtherSelection));
            RefreshEditor();
            UpdatePreview();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void AddStation()
    {
        if (_loadFailed) return;
        SelectedStation = null;
        _addingStation = true;
        StationProfileDraft = ProfileChoices[0];
        StationRuleDraft = RuleChoices[0];
        NameDraft = "";
        UrlDraft = "";
        SelectedTabIndex = 0;
        RefreshEditor();
        Message = Localization.Localizer.Current.Message("StationEntryHint");
    }

    private void SaveStation()
    {
        try
        {
            var original = _addingStation ? new Core.Models.Station() : SelectedStation!.Data;
            var requestedName = NameDraft.Trim();
            var updated = Core.Models.StationCatalog.Edit(original, NameDraft, UrlDraft, _stations.Select(s => s.Data));
            updated = updated with { RecordingProfileId = StationProfileDraft?.Id, MetadataRuleSetId = StationRuleDraft?.Id };
            var affectsStationList = _addingStation || updated.Name != original.Name;
            var result = _stations.Select(s => s.Id == updated.Id ? updated : s.Data).ToList();
            if (_addingStation) result.Add(updated);
            // The configuration exists before any station file can reference entries.
            EnsureConfigurationExists();
            SaveStationCollection(result);
            var entry = _stations.FirstOrDefault(s => s.Id == updated.Id);
            if (entry is null) entry = Attach(updated);
            else entry.Update(updated);
            _addingStation = false;
            if (affectsStationList) RefreshStations();
            SelectedStation = entry;
            NameDraft = updated.Name;
            UrlDraft = updated.Url;
            RefreshEditor();
            UpdatePreview();
            Message = updated.Name == requestedName ? Localization.Localizer.Current.Message("StationSaved") : Localization.Localizer.Current.Message("CollisionSave", updated.Name);
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("StationSaveFailed", exception.Message); }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanRemoveStation))]
    private async Task RemoveStationAsync()
    {
        var station = SelectedStation;
        if (station is null) return;
        try
        {
            if (ConfirmCatalogDeletion is null || !await ConfirmCatalogDeletion(new(false, station.Name, []) { IsStation = true })) return;
            if (station.IsActive || !_stations.Contains(station)) return;
            SaveStationCollection(_stations.Where(s => s != station).Select(s => s.Data).ToList());
            station.PropertyChanged -= StationChanged;
            _stations.Remove(station);
            station.Dispose();
            SelectedStation = null;
            RefreshStations();
            SelectedStation = VisibleStations.FirstOrDefault();
            Message = Localization.Localizer.Current.Message("StationRemoved");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("StationRemoveFailed", exception.Message); }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SaveSettings()
    {
        if (_loadFailed || _savingSettings) return;
        _savingSettings = true;
        SettingsError = "";
        try
        {
            if (string.IsNullOrWhiteSpace(ExecutablePath) || string.IsNullOrWhiteSpace(RecordingRoot))
                throw new ArgumentException(Localization.Localizer.Current.Message("SettingsPathsRequired"));
            var executable = _store.PathForStorage(ExecutablePath, AppContext.BaseDirectory);
            var recording = _store.PathForStorage(RecordingRoot, AppContext.BaseDirectory);
            var copy = _configuration.DeepClone().AsObject();
            copy["application"]!["streamripperPath"] = executable;
            copy["application"]!["language"] = LanguageMode;
            copy["application"]!["theme"] = ThemeMode;
            copy["recording"]!["directory"] = recording;
            _store.SaveConfiguration(copy);
            _configuration = copy;
            ExecutablePath = copy["application"]!["streamripperPath"]!.GetValue<string>();
            RecordingRoot = recording;
            if (SelectedStation is { } station) UpdateRecordingDestination(station);
            Message = Localization.Localizer.Current.Message("SettingsSaved");
            UpdatePreview();
        }
        catch (Exception exception) { SettingsError = Message = Localization.Localizer.Current.Message("SettingsSaveFailed", exception.Message); }
        finally { _savingSettings = false; }
    }

    private void EnsureConfigurationExists()
    {
        if (!File.Exists(Path.Combine(_store.DirectoryPath, "config.json")))
            _store.SaveConfiguration(_configuration);
    }

    public void ReportPathError(Exception exception)
    {
        SettingsError = Message = Localization.Localizer.Current.Message(
            exception is DirectoryNotFoundException ? "FolderDoesNotExist" : "PathSelectionFailed", exception.Message);
    }
    public void ReportRelayError(string message) => Message = Localization.Localizer.Current.Message("RelayActionError", message);

    private void SaveStationCollection(List<Core.Models.Station> stations)
    {
        var file = new Infrastructure.StationFile { Stations = stations, AdditionalData = _stationFile.AdditionalData };
        _store.SaveStations(file);
        _stationFile = file;
    }

    private StationViewModel Attach(Core.Models.Station station)
    {
        var entry = new StationViewModel(station);
        entry.Recording.PrepareStart = () =>
        {
            if (SelectedStation == entry)
            {
                SaveStationOnBlur();
                if (HasStationChanges) throw new InvalidOperationException(Localization.Localizer.Current.Message("StationChangesInvalid"));
            }
            if (_loadFailed) throw new InvalidOperationException(Localization.Localizer.Current.Message("ConfigurationFixBeforeRecording"));
            entry.Recording.MetadataRulesPath = ResolveRules(entry);
            entry.Recording.ExecutablePath = ExecutablePath;
            entry.Recording.RecordingRoot = RecordingRoot;
            entry.Recording.Options = ResolveLaunchOptions(entry, checkCaFile: true);
        };
        entry.Recording.StreamObserved = observation => SaveObservation(entry, observation);
        entry.PropertyChanged += StationChanged;
        _stations.Add(entry);
        return entry;
    }

    private void StationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StationViewModel.IsActive))
        {
            RefreshStations();
            RefreshEditor();
            OnPropertyChanged(nameof(HasActiveRecordings));
            if (sender is StationViewModel { IsActive: true } station) StationPinned?.Invoke(station);
        }
    }

    private void RefreshStations(string? query = null)
    {
        var visible = Core.Models.StationCatalog.Visible(_stations, s => s.Name, s => s.Id, s => s.IsActive, query ?? SearchText);
        _refreshingList = true;
        try
        {
            for (var index = VisibleStations.Count - 1; index >= 0; index--)
                if (!visible.Contains(VisibleStations[index])) VisibleStations.RemoveAt(index);
            for (var index = 0; index < visible.Count; index++)
            {
                var previous = VisibleStations.IndexOf(visible[index]);
                if (previous < 0) VisibleStations.Insert(index, visible[index]);
                else if (previous != index) VisibleStations.Move(previous, index);
            }
        }
        finally { _refreshingList = false; }
        OnPropertyChanged(nameof(SelectedStation));
        OnPropertyChanged(nameof(ListSelection));
        RefreshStationGroups();
    }

    private void RefreshEditor()
    {
        OnPropertyChanged(nameof(CanSaveStation));
        OnPropertyChanged(nameof(CanRemoveStation));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(CanCreateStationProfile));
        CreateStationProfileCommand.NotifyCanExecuteChanged();
        RefreshProfileCommands();
        RefreshRuleCommands();
        RemoveStationCommand.NotifyCanExecuteChanged();
    }

    public async Task<bool> PrepareCloseAsync()
    {
        var results = await Task.WhenAll(_stations.Select(s => s.Recording.PrepareCloseAsync()));
        return results.All(stopped => stopped);
    }

    public void Dispose()
    {
        ConfirmCatalogDeletion = null;
        ConfirmUnsavedChanges = null;
        ProfileOptions.PropertyChanged -= ProfileOptionsChanged;
        foreach (var station in _stations)
        {
            station.PropertyChanged -= StationChanged;
            station.Dispose();
        }
        _emptyConsole.Dispose();
        if (Rules is not null)
        {
            Rules.PropertyChanged -= RulesSelectionChanged;
            Rules.Dispose();
        }
        Localization.Localizer.Current.LanguageChanged -= LanguageChanged;
    }
}
