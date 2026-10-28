namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public Func<string, Task<UnsavedChangesChoice>>? ConfirmUnsavedChanges { get; set; }
    private bool _confirmingDraft;
    private bool _acceptProfileSelection;
    private bool _changingProfileSelection;
    private bool _acceptStationSelection;
    private bool _changingStationSelection;
    private bool _changingEditorTab;

    private async Task<UnsavedChangesChoice> AskAboutDraftAsync(string name)
    {
        if (_confirmingDraft || ConfirmUnsavedChanges is null) return UnsavedChangesChoice.Cancel;
        _confirmingDraft = true;
        try
        {
            return await ConfirmUnsavedChanges(name);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError("Opening unsaved changes confirmation failed: {0}", exception);
            return UnsavedChangesChoice.Cancel;
        }
        finally
        {
            _confirmingDraft = false;
        }
    }

    private async Task<bool> ConfirmProfileChangesAsync()
    {
        if (!ProfileHasChanges()) return true;
        var choice = await AskAboutDraftAsync(ProfileNameDraft);
        if (choice == UnsavedChangesChoice.Discard) return true;
        if (choice != UnsavedChangesChoice.Save) return false;
        SaveProfile();
        return !ProfileHasChanges();
    }

    private async Task<bool> ConfirmRulesChangesAsync()
    {
        if (!Rules.IsDirty) return true;
        var choice = await AskAboutDraftAsync(Rules.Name);
        if (choice == UnsavedChangesChoice.Discard) return true;
        if (choice != UnsavedChangesChoice.Save || Rules.CheckAndApplyCommand.IsRunning) return false;
        await Rules.CheckAndApplyCommand.ExecuteAsync(null);
        return Rules.LastAppliedId is not null && !Rules.IsDirty;
    }

    private void ApplyProfileSelection(ProfileChoice? value)
    {
        _acceptProfileSelection = true;
        try
        {
            SelectedProfile = value;
        }
        finally
        {
            _acceptProfileSelection = false;
        }
    }

    public async Task<bool> SelectProfileAsync(ProfileChoice? value)
    {
        if (value == SelectedProfile) return true;
        if (_changingProfileSelection) return false;
        _changingProfileSelection = true;
        try
        {
            if (!await ConfirmProfileChangesAsync()) return false;
            ApplyProfileSelection(value);
            return true;
        }
        finally
        {
            _changingProfileSelection = false;
            OnPropertyChanged(nameof(SelectedProfile));
        }
    }

    private Task<bool> SelectStationEditorAsync(StationViewModel? station, int tab) => station is null
        ? Task.FromResult(true)
        : tab switch
        {
            1 => SelectProfileAsync(SharedProfiles.FirstOrDefault(p => p.Id == station?.Data.RecordingProfileId)),
            2 => Rules.SelectAsync(Rules.RuleSets.FirstOrDefault(r => r.Id == station?.Data.MetadataRuleSetId)),
            _ => Task.FromResult(true)
        };

    public async Task<bool> SelectStationAsync(StationViewModel? station)
    {
        if (station == SelectedStation) return true;
        if (_changingStationSelection) return false;
        _changingStationSelection = true;
        try
        {
            if (!await SelectStationEditorAsync(station, SelectedTabIndex)) return false;
            _acceptStationSelection = true;
            SelectedStation = station;
            return true;
        }
        finally
        {
            _acceptStationSelection = false;
            _changingStationSelection = false;
            OnPropertyChanged(nameof(SelectedStation));
            OnPropertyChanged(nameof(ListSelection));
            OnPropertyChanged(nameof(PinnedSelection));
            OnPropertyChanged(nameof(OtherSelection));
        }
    }

    partial void OnSelectedTabIndexChanged(int oldValue, int newValue)
    {
        if (_initialized && !_changingEditorTab) _ = SelectEditorTabAsync(oldValue, newValue);
    }

    private async Task SelectEditorTabAsync(int previous, int requested)
    {
        _changingEditorTab = true;
        try
        {
            if (!await SelectStationEditorAsync(SelectedStation, requested)) SelectedTabIndex = previous;
        }
        finally
        {
            _changingEditorTab = false;
        }
    }
}