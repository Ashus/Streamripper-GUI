namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public bool CanImportStations => !_loadFailed;
    public void ReportImportError(string message) => SettingsError = Message = message;

    public OpmlImportViewModel CreateOpmlImport() => new(
        (entries, replace) => Core.Models.OpmlImportPlanner.Prepare(_stations.Select(s => s.Data).ToArray(), entries, replace),
        ImportBlockingReason,
        ApplyOpmlImport);

    private string? ImportBlockingReason(Core.Models.OpmlImportPlan plan)
    {
        if (_loadFailed) return Localization.Localizer.Current.Message("ConfigurationFixBeforeImport");
        var retained = plan.Stations.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var removingActive = _stations.Where(s => s.IsActive && !retained.Contains(s.Id)).Select(s => s.Name).ToArray();
        return removingActive.Length == 0 ? null
            : Localization.Localizer.Current.Message("OpmlActiveRemovalBlocked", string.Join(", ", removingActive));
    }

    private void ApplyOpmlImport(IReadOnlyList<Core.Models.OpmlStation> entries, bool replace)
    {
        // Reconcile again at commit time: recordings may have updated observations while preview was open.
        var plan = Core.Models.OpmlImportPlanner.Prepare(_stations.Select(s => s.Data).ToArray(), entries, replace);
        var blocked = ImportBlockingReason(plan);
        if (blocked is not null) throw new InvalidOperationException(blocked);
        EnsureConfigurationExists();
        SaveStationCollection(plan.Stations.ToList());
        var selectedId = SelectedStation?.Id;
        var retainedIds = plan.Stations.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var removed in _stations.Where(s => !retainedIds.Contains(s.Id)).ToArray())
        {
            removed.PropertyChanged -= StationChanged;
            _stations.Remove(removed);
            removed.Dispose();
        }
        foreach (var data in plan.Stations)
        {
            var existing = _stations.FirstOrDefault(s => s.Id == data.Id);
            if (existing is null) Attach(data);
            else existing.Update(data);
        }
        _addingStation = false;
        SearchText = "";
        RefreshStations();
        SelectedStation = _stations.FirstOrDefault(s => s.Id == selectedId) ?? VisibleStations.FirstOrDefault();
        NameDraft = SelectedStation?.Name ?? "";
        UrlDraft = SelectedStation?.Url ?? "";
        StationProfileDraft = ProfileChoices.FirstOrDefault(p => p.Id == SelectedStation?.Data.RecordingProfileId);
        StationRuleDraft = RuleChoices.FirstOrDefault(p => p.Id == SelectedStation?.Data.MetadataRuleSetId);
        RefreshEditor();
        UpdatePreview();
        Message = Localization.Localizer.Current.Message("OpmlCompleted", plan.Changes.Count(c => c.Action == "Add"), plan.Changes.Count(c => c.Action == "Update"), plan.Changes.Count(c => c.Action == "Remove"));
    }
}
