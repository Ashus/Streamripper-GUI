namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private ProfileChoice? _selectedProfile;
    private Core.Models.RecordingOptions _savedDefaults = new();

    public System.Collections.ObjectModel.ObservableCollection<ProfileChoice> SharedProfiles { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<ProfileChoice> ProfileChoices { get; } = [new(null, Localization.Localizer.Current.Message("Default"))];
    public RecordingOptionsViewModel ProfileOptions { get; } = new();
    public bool CanCreateStationProfile => CanSaveStation && SelectedStation is { Data.RecordingProfileId: null } && StationProfileDraft?.Id is null;
    public bool CanDeleteProfile => !_loadFailed && SelectedProfile?.Id is not null;
    public bool CanEditProfile => !_loadFailed && SelectedProfile is not null;
    public bool CanSaveProfile => CanEditProfile && ProfileHasChanges();
    private bool ProfileHasChanges()
    {
        if (SelectedProfile is null) return false;
        if (SelectedProfile.Id is not null && ProfileNameDraft.Trim() != SelectedProfile.Name) return true;
        try { return !System.Text.Json.Nodes.JsonNode.DeepEquals(Core.Models.RecordingOptionsJson.Write(ProfileOptions.Snapshot()), Core.Models.RecordingOptionsJson.Write(ResolveOptions(SelectedProfile.Id))); }
        catch (ArgumentException) { return true; }
    }
    public bool CanRenameProfile => SelectedProfile?.Id is not null;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial ProfileChoice? StationProfileDraft { get; set; }

    partial void OnStationProfileDraftChanged(ProfileChoice? value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanCreateStationProfile));
        CreateStationProfileCommand.NotifyCanExecuteChanged();
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string ProfileNameDraft { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string CommandPreview { get; private set; } = "";

    public ProfileChoice? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!_acceptProfileSelection && (ProfileHasChanges() || _changingProfileSelection))
            {
                if (value != _selectedProfile) _ = SelectProfileAsync(value);
                return;
            }
            if (!SetProperty(ref _selectedProfile, value)) return;
            if (value is not null)
            {
                ProfileNameDraft = value.Name;
                ProfileOptions.SetBaseline(value.Id is null ? new Core.Models.RecordingOptions() : _savedDefaults);
                ProfileOptions.Load(ResolveOptions(value.Id));
            }
            RefreshProfileCommands();
        }
    }

    private void LoadProfiles()
    {
        _savedDefaults = Core.Models.RecordingOptionsJson.Read(_configuration["recording"]!["defaults"]);
        ProfileOptions.PropertyChanged += ProfileOptionsChanged;
        Core.Models.RecordingArguments.Validate(_savedDefaults);
        SharedProfiles.Add(ProfileChoices[0]);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _configuration["recording"]!["profiles"]!.AsArray())
        {
            var id = node?["id"]?.GetValue<string>();
            var name = node?["name"]?.GetValue<string>().Trim();
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || string.IsNullOrWhiteSpace(name) || !names.Add(name))
                throw new InvalidDataException(Localization.Localizer.Current.Message("ProfileCatalogInvalid"));
            Core.Models.RecordingArguments.Validate(Core.Models.RecordingOptionsJson.Read(node!["options"]));
            var choice = new ProfileChoice(id, name);
            SharedProfiles.Add(choice);
            ProfileChoices.Add(choice);
        }
        SortProfiles();
        SelectedProfile = SharedProfiles.FirstOrDefault();
    }

    private Core.Models.RecordingOptions ResolveOptions(string? id)
    {
        if (id is null) return _savedDefaults;
        var node = _configuration["recording"]!["profiles"]!.AsArray()
            .FirstOrDefault(p => p?["id"]?.GetValue<string>() == id);
        return Core.Models.RecordingOptionsJson.Read(node?["options"]);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task NewProfileAsync()
    {
        if (_loadFailed) return;
        if (!await ConfirmProfileChangesAsync()) return;
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var name = UniqueProfileName(Localization.Localizer.Current.Format("NewProfile"));
            PersistProfile(id, name, NormalizeProfilePaths(ResolveOptions(SelectedProfile?.Id) with { }));
            ApplyProfileSelection(SharedProfiles.Single(p => p.Id == id));
            Message = Localization.Localizer.Current.Message("ProfileCreated");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("ProfileSaveFailed", exception.Message); }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private void SaveProfile()
    {
        if (!CanSaveProfile) return;
        try
        {
            if (SelectedProfile is { Id: null })
            {
                var defaults = NormalizeProfilePaths(ProfileOptions.Snapshot());
                var configuration = _configuration.DeepClone().AsObject();
                configuration["recording"]!["defaults"] = Core.Models.RecordingOptionsJson.Write(defaults);
                _store.SaveConfiguration(configuration);
                _configuration = configuration;
                _savedDefaults = defaults;
                ProfileOptions.Load(defaults);
                RefreshProfileCommands();
                UpdatePreview();
                Message = Localization.Localizer.Current.Message("DefaultProfileSaved");
                return;
            }
            var name = ProfileNameDraft.Trim();
            if (name.Length == 0) throw new ArgumentException(Localization.Localizer.Current.Message("ProfileNameRequired"));
            var id = SelectedProfile?.Id ?? Guid.NewGuid().ToString("N");
            if (SharedProfiles.Any(p => p.Id != id && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(Localization.Localizer.Current.Message("ProfileNameExists"));
            var options = NormalizeProfilePaths(ProfileOptions.Snapshot());
            PersistProfile(id, name, options);
            ProfileOptions.Load(options);
            SelectedProfile = SharedProfiles.Single(p => p.Id == id);
            ProfileNameDraft = name;
            RefreshProfileCommands();
            UpdatePreview();
            Message = Localization.Localizer.Current.Message("ProfileSaved");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("ProfileSaveFailed", exception.Message); }
    }

    private void PersistProfile(string id, string name, Core.Models.RecordingOptions options)
    {
        var copy = _configuration.DeepClone().AsObject();
        var array = copy["recording"]!["profiles"]!.AsArray();
        var node = array.FirstOrDefault(p => p?["id"]?.GetValue<string>() == id) as System.Text.Json.Nodes.JsonObject;
        if (node is null)
        {
            node = new System.Text.Json.Nodes.JsonObject { ["id"] = id };
            array.Add(node);
        }
        node["name"] = name;
        node["options"] = Core.Models.RecordingOptionsJson.Write(options);
        _store.SaveConfiguration(copy);
        _configuration = copy;
        var existing = SharedProfiles.FirstOrDefault(p => p.Id == id);
        if (existing is null)
        {
            existing = new ProfileChoice(id, name);
            SharedProfiles.Add(existing);
            ProfileChoices.Add(existing);
        }
        else existing.Name = name;
        SortProfiles();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanCreateStationProfile))]
    private async Task CreateStationProfileAsync()
    {
        var station = SelectedStation;
        if (!CanCreateStationProfile || station is null) return;
        if (!await ConfirmProfileChangesAsync()) return;
        try
        {
            var id = Guid.NewGuid().ToString("N");
            var name = UniqueProfileName(station.Name);
            var options = ResolveOptions(station.Data.RecordingProfileId) with { };
            // Persist the profile first. A failed station write can only leave an unused profile.
            PersistProfile(id, name, options);
            var updated = station.Data with { RecordingProfileId = id };
            SaveStationCollection(_stations.Select(s => s.Id == station.Id ? updated : s.Data).ToList());
            station.Update(updated);
            StationProfileDraft = ProfileChoices.Single(p => p.Id == id);
            ApplyProfileSelection(SharedProfiles.Single(p => p.Id == id));
            SelectedTabIndex = 1;
            UpdatePreview();
            RefreshEditor();
            Message = Localization.Localizer.Current.Message("StationProfileCreated");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("StationProfileCreateFailed", exception.Message); }
    }

    private string UniqueProfileName(string requested)
    {
        var name = requested;
        var suffix = 2;
        while (SharedProfiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = $"{requested} ({suffix++})";
        return name;
    }

    private void SortProfiles()
    {
        CatalogOrder.Sort(SharedProfiles, profile => profile.Id, profile => profile.Name);
        CatalogOrder.Sort(ProfileChoices, profile => profile.Id, profile => profile.Name);
    }

    private void SaveObservation(StationViewModel station, Core.Models.StreamObservation observation)
    {
        if (_loadFailed || station.Data.Url != observation.Url || !_stations.Contains(station)) return;
        var updated = station.Data with { LastObservedStream = observation.ToJson() };
        station.Update(updated);
        try
        {
            SaveStationCollection(_stations.Select(s => s.Data).ToList());
            UpdatePreview();
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("StreamObservationSaveFailed", exception.Message); }
    }

    private void RefreshProfileCommands()
    {
        OnPropertyChanged(nameof(CanEditProfile));
        OnPropertyChanged(nameof(CanSaveProfile));
        OnPropertyChanged(nameof(CanRenameProfile));
        SaveProfileCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanDeleteProfile));
        DeleteProfileCommand.NotifyCanExecuteChanged();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private async Task DeleteProfileAsync()
    {
        if (!CanDeleteProfile) return;
        try
        {
            var choice = SelectedProfile!;
            if (!await ConfirmDeletionAsync(choice.Id!, choice.Name, false)) return;
            if (_loadFailed || !SharedProfiles.Contains(choice)) return;
            DetachCatalogReference(choice.Id!, false);
            var copy = _configuration.DeepClone().AsObject();
            var catalog = copy["recording"]!["profiles"]!.AsArray();
            catalog.Remove(catalog.Single(n => n?["id"]?.GetValue<string>() == choice.Id));
            _store.SaveConfiguration(copy);
            _configuration = copy;
            SharedProfiles.Remove(choice);
            ProfileChoices.Remove(choice);
            ApplyProfileSelection(ProfileChoices[0]);
            Message = Localization.Localizer.Current.Message("ProfileRemoved");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("DeletionError", exception.Message); }
    }

    private Core.Models.RecordingOptions NormalizeProfilePaths(Core.Models.RecordingOptions options) =>
        string.IsNullOrWhiteSpace(options.TlsCaFile) ? options : options with { TlsCaFile = _store.PathForStorage(options.TlsCaFile, AppContext.BaseDirectory) };

    private Core.Models.RecordingOptions ResolveLaunchOptions(StationViewModel station, bool checkCaFile = false)
    {
        var options = ResolveOptions(station.Data.RecordingProfileId) with { };
        if (options.TlsCaFile.Length != 0)
        {
            var path = Path.GetFullPath(options.TlsCaFile, AppContext.BaseDirectory);
            if (checkCaFile && !File.Exists(path)) throw new FileNotFoundException(Localization.Localizer.Current.Message("CaMissing"), path);
            options = options with { TlsCaFile = path };
        }
        var observation = station.Data.LastObservedStream;
        var audio = observation?["audioFormat"]?.GetValue<string>();
        if (audio is not null && audio is not ("unknown" or "MP3")) options = options with { Wav = false };
        if (audio == "FLAC" || observation?["transport"]?.GetValue<string>() == "HLS")
            options = options with { RelayEnabled = false };
        if (audio == "AAC" && options.SilenceDetector == "alternative")
            options = options with { SilenceDetector = "standard" };
        return options;
    }

    private void UpdatePreview()
    {
        if (SelectedStation is null) { CommandPreview = Localization.Localizer.Current.Message("PreviewMissing"); return; }
        try
        {
            var executable = Path.GetFullPath(ExecutablePath, AppContext.BaseDirectory);
            var destination = Path.Combine(Path.GetFullPath(RecordingRoot, AppContext.BaseDirectory), Core.Models.StationCatalog.DirectoryName(SelectedStation.Name));
            var options = ResolveLaunchOptions(SelectedStation);
            var rules = SelectedStation.Data.MetadataRuleSetId is { } ruleId &&
                SelectedStation.Data.LastObservedStream?["metadataRules"]?.GetValue<string>() != "unsupported"
                ? new Infrastructure.MetadataRuleFiles(_store.DirectoryPath).Resolve(RuleChoices.Single(r => r.Id == ruleId).File) : null;
            var args = Core.Models.RecordingArguments.Build(SelectedStation.Url, destination, options, rules);
            CommandPreview = string.Join(" ", new[] { executable }.Concat(args).Select(Services.RecordingProcess.QuoteArgument));
        }
        catch (Exception exception) { CommandPreview = Localization.Localizer.Current.Message("PreviewError", exception.Message); }
    }
}

public sealed class ProfileChoice(string? id, string name) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string? Id { get; } = id;
    private string _name = name;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public override string ToString() => Name;
}
