namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public System.Collections.ObjectModel.ObservableCollection<StationViewModel> PinnedStations { get; } = [];
    public System.Collections.ObjectModel.ObservableCollection<StationViewModel> OtherStations { get; } = [];
    public bool HasPinnedStations => PinnedStations.Count != 0;
    public event Action<StationViewModel>? StationPinned;

    public StationViewModel? PinnedSelection
    {
        get => SelectedStation is { IsActive: true } ? SelectedStation : null;
        set { if (!_refreshingList && value is not null) SelectedStation = value; }
    }

    public StationViewModel? OtherSelection
    {
        get => SelectedStation is { IsActive: false } && OtherStations.Contains(SelectedStation) ? SelectedStation : null;
        set { if (!_refreshingList && value is not null) SelectedStation = value; }
    }

    private static void SyncStations(System.Collections.ObjectModel.ObservableCollection<StationViewModel> collection, IEnumerable<StationViewModel> source)
    {
        var items = source.ToArray();
        for (var index = collection.Count - 1; index >= 0; index--)
            if (!items.Contains(collection[index])) collection.RemoveAt(index);
        for (var index = 0; index < items.Length; index++)
        {
            var previous = collection.IndexOf(items[index]);
            if (previous < 0) collection.Insert(index, items[index]);
            else if (previous != index) collection.Move(previous, index);
        }
    }

    private void RefreshStationGroups()
    {
        _refreshingList = true;
        try
        {
            SyncStations(PinnedStations, VisibleStations.Where(s => s.IsActive));
            SyncStations(OtherStations, VisibleStations.Where(s => !s.IsActive));
        }
        finally { _refreshingList = false; }
        OnPropertyChanged(nameof(HasPinnedStations));
        OnPropertyChanged(nameof(PinnedSelection));
        OnPropertyChanged(nameof(OtherSelection));
    }

    public async Task ToggleStationAsync(StationViewModel station)
    {
        if (!await SelectStationAsync(station)) return;
        if (station.IsActive)
        {
            if (station.Recording.StopCommand.CanExecute(null)) await station.Recording.StopCommand.ExecuteAsync(null);
        }
        else if (station.Recording.StartCommand.CanExecute(null)) station.Recording.StartCommand.Execute(null);
    }

    public void RemoveStationFromList(StationViewModel station)
    {
        _ = RemoveStationFromListAsync(station);
    }

    private async Task RemoveStationFromListAsync(StationViewModel station)
    {
        if (!await SelectStationAsync(station)) return;
        if (RemoveStationCommand.CanExecute(null)) RemoveStationCommand.Execute(null);
    }
}
