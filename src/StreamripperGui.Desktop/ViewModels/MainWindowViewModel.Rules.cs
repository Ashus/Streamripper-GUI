namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public RulesEditorViewModel Rules { get; private set; } = null!;
    public System.Collections.ObjectModel.ObservableCollection<RuleSetChoice> RuleChoices { get; } = [new(null, Localization.Localizer.Current.Message("None"), "")];
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial RuleSetChoice? StationRuleDraft { get; set; }

    public bool CanCreateStationFilter => CanSaveStation && SelectedStation is { Data.MetadataRuleSetId: null } && StationRuleDraft?.Id is null;
    public bool CanDeleteFilter => !_loadFailed && Rules is { Selected.Id: not null };

    partial void OnStationRuleDraftChanged(RuleSetChoice? value)
    {
        _ = value;
        RefreshRuleCommands();
    }

    private void RefreshRuleCommands()
    {
        OnPropertyChanged(nameof(CanCreateStationFilter));
        OnPropertyChanged(nameof(CanDeleteFilter));
        CreateStationFilterCommand.NotifyCanExecuteChanged();
        DeleteFilterCommand.NotifyCanExecuteChanged();
    }

    private void RulesSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RulesEditorViewModel.Selected)) RefreshRuleCommands();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanCreateStationFilter))]
    private async Task CreateStationFilterAsync()
    {
        var station = SelectedStation;
        if (!CanCreateStationFilter || station is null || Rules.CheckAndApplyCommand.IsRunning) return;
        try
        {
            if (!await Rules.NewDraftAsync()) return;
            var name = station.Name;
            var suffix = 2;
            while (RuleChoices.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{station.Name} ({suffix++})";
            Rules.Name = name;
            await Rules.CheckAndApplyCommand.ExecuteAsync(null);
            if (Rules.Selected is not { Id: { } id } || Rules.LastAppliedId != id) { Message = Rules.Status; return; }
            if (!_stations.Contains(station) || station.IsActive || station.Data.MetadataRuleSetId is not null)
                throw new InvalidOperationException(Localization.Localizer.Current.Message("FilterStationChanged"));
            var updated = station.Data with { MetadataRuleSetId = id };
            SaveStationCollection(_stations.Select(s => s.Id == station.Id ? updated : s.Data).ToList());
            station.Update(updated);
            if (SelectedStation == station) StationRuleDraft = RuleChoices.Single(r => r.Id == id);
            RefreshEditor();
            SelectedTabIndex = 2;
            Message = Localization.Localizer.Current.Message("FilterAssigned");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("RulesSaveFailed", exception.Message); }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanDeleteFilter))]
    private async Task DeleteFilterAsync()
    {
        if (!CanDeleteFilter || Rules.CheckAndApplyCommand.IsRunning) return;
        try
        {
            var choice = Rules.Selected!;
            if (!await ConfirmDeletionAsync(choice.Id!, choice.Name, true)) return;
            if (_loadFailed || !Rules.RuleSets.Contains(choice) || Rules.CheckAndApplyCommand.IsRunning) return;
            DetachCatalogReference(choice.Id!, true);
            var path = new Infrastructure.MetadataRuleFiles(_store.DirectoryPath).Resolve(choice.File);
            var staged = path + "." + Guid.NewGuid().ToString("N") + ".delete-tmp";
            var hadFile = File.Exists(path);
            if (hadFile) File.Move(path, staged);
            try
            {
                var copy = _configuration.DeepClone().AsObject();
                var catalog = copy["metadataRuleSets"]!.AsArray();
                catalog.Remove(catalog.Single(n => n?["id"]?.GetValue<string>() == choice.Id));
                _store.SaveConfiguration(copy);
                _configuration = copy;
            }
            catch
            {
                if (hadFile) File.Move(staged, path);
                throw;
            }
            RuleChoices.Remove(choice);
            Rules.RuleSets.Remove(choice);
            Rules.SelectAfterDeletion(Rules.RuleSets.FirstOrDefault());
            RefreshRuleCommands();
            if (hadFile) File.Delete(staged);
            Message = Localization.Localizer.Current.Message("FilterDeletedWithFile");
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("DeletionError", exception.Message); }
    }

    private void LoadRules()
    {
        Rules = new RulesEditorViewModel(_store.DirectoryPath,
            () => Path.GetFullPath(ExecutablePath, AppContext.BaseDirectory), SaveRuleCatalog);
        Rules.IsRulesFileActive = path => _stations.Any(s => s.Recording.UsesRulesFile(path));
        Rules.ConfirmLeaveDraft = ConfirmRulesChangesAsync;
        Rules.PropertyChanged += RulesSelectionChanged;
        foreach (var node in _configuration["metadataRuleSets"]!.AsArray())
        {
            var id = node?["id"]?.GetValue<string>() ?? throw new InvalidDataException(Localization.Localizer.Current.Message("FilterIdMissing"));
            var name = node["name"]?.GetValue<string>() ?? throw new InvalidDataException(Localization.Localizer.Current.Message("FilterNameMissing"));
            var file = node["file"]?.GetValue<string>() ?? throw new InvalidDataException(Localization.Localizer.Current.Message("FilterFileMissing"));
            new Infrastructure.MetadataRuleFiles(_store.DirectoryPath).Resolve(file);
            if (RuleChoices.Any(r => r.Id == id)) throw new InvalidDataException(Localization.Localizer.Current.Message("FilterIdsNotUnique"));
            var choice = new RuleSetChoice(id, name, file);
            Rules.RuleSets.Add(choice);
            RuleChoices.Add(choice);
        }
        CatalogOrder.Sort(RuleChoices, rule => rule.Id, rule => rule.Name);
        CatalogOrder.Sort(Rules.RuleSets, rule => rule.Id, rule => rule.Name);
    }

    private void SaveRuleCatalog(RuleSetChoice choice, string name)
    {
        if (_loadFailed) throw new InvalidOperationException(Localization.Localizer.Current.Message("ConfigurationFixRequired"));
        var copy = _configuration.DeepClone().AsObject();
        var catalog = copy["metadataRuleSets"]!.AsArray();
        var node = catalog.FirstOrDefault(n => n?["id"]?.GetValue<string>() == choice.Id) as System.Text.Json.Nodes.JsonObject;
        if (node is null) { node = new(); catalog.Add(node); }
        node["id"] = choice.Id;
        node["name"] = name;
        node["file"] = choice.File;
        _store.SaveConfiguration(copy);
        _configuration = copy;
        var existing = RuleChoices.FirstOrDefault(r => r.Id == choice.Id);
        if (existing is null) RuleChoices.Add(choice);
        else existing.Name = name;
        CatalogOrder.Sort(RuleChoices, rule => rule.Id, rule => rule.Name);
        UpdatePreview();
    }

    private string? ResolveRules(StationViewModel station)
    {
        if (station.Data.MetadataRuleSetId is null) return null;
        var selected = RuleChoices.Single(r => r.Id == station.Data.MetadataRuleSetId);
        var files = new Infrastructure.MetadataRuleFiles(_store.DirectoryPath);
        var content = files.Read(selected.File);
        using var validator = new Infrastructure.GlibRegexValidator(Path.GetFullPath(ExecutablePath, AppContext.BaseDirectory));
        var validation = Core.Models.MetadataRules.Validate(content, validator);
        if (validation.Diagnostics.Any(d => !d.IsWarning))
            throw new InvalidDataException(Localization.Localizer.Current.Message("InvalidRuleFile"));
        if (station.Data.LastObservedStream?["metadataRules"]?.GetValue<string>() == "unsupported") return null;
        return files.Resolve(selected.File);
    }
}
