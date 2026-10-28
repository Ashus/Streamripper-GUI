namespace StreamripperGui.Desktop.ViewModels;

public sealed record CatalogDeletionRequest(bool IsFilter, string Name, IReadOnlyList<string> Stations)
{
    public bool HasStations => Stations.Count != 0;
    public bool IsStation { get; init; }
    public bool IsProfile => !IsFilter && !IsStation;
    public bool ShowProfileDetach => IsProfile && HasStations;
    public bool ShowFilterDetach => IsFilter && HasStations;
    public bool ShowUnused => !IsStation && !HasStations;
}

public sealed partial class MainWindowViewModel
{
    public Func<CatalogDeletionRequest, Task<bool>>? ConfirmCatalogDeletion { get; set; }

    private StationViewModel[] ReferencingStations(string id, bool filter) =>
        _stations.Where(s => (filter ? s.Data.MetadataRuleSetId : s.Data.RecordingProfileId) == id)
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Id).ToArray();

    private async Task<bool> ConfirmDeletionAsync(string id, string name, bool filter)
    {
        var affected = ReferencingStations(id, filter).Select(s => (s.Id, s.Name)).ToArray();
        if (ConfirmCatalogDeletion is null || !await ConfirmCatalogDeletion(new(filter, name, affected.Select(s => s.Name).ToArray()))) return false;
        if (!affected.SequenceEqual(ReferencingStations(id, filter).Select(s => (s.Id, s.Name))))
            throw new InvalidOperationException(Localization.Localizer.Current.Message("DeletionChanged"));
        return true;
    }

    private void DetachCatalogReference(string id, bool filter)
    {
        var affected = ReferencingStations(id, filter);
        if (affected.Length != 0)
        {
            var updated = _stations.Select(s => (filter ? s.Data.MetadataRuleSetId : s.Data.RecordingProfileId) == id
                ? filter ? s.Data with { MetadataRuleSetId = null } : s.Data with { RecordingProfileId = null }
                : s.Data).ToList();
            // Detach references first, while the catalog entry still exists.
            SaveStationCollection(updated);
            foreach (var station in affected) station.Update(updated.Single(s => s.Id == station.Id));
        }
        if (filter && StationRuleDraft?.Id == id) StationRuleDraft = RuleChoices[0];
        if (!filter && StationProfileDraft?.Id == id) StationProfileDraft = ProfileChoices[0];
        RefreshEditor();
        UpdatePreview();
    }
}
