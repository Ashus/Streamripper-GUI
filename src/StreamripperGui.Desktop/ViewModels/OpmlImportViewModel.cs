namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class OpmlImportViewModel(
    Func<IReadOnlyList<Core.Models.OpmlStation>, bool, Core.Models.OpmlImportPlan> prepare,
    Func<Core.Models.OpmlImportPlan, string?> blockingReason,
    Action<IReadOnlyList<Core.Models.OpmlStation>, bool> apply)
    : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private Core.Models.OpmlImportPlan? _plan;
    private bool _updatingSelection;

    public System.Collections.ObjectModel.ObservableCollection<OpmlSelection> Entries { get; } = [];
    public IReadOnlyList<Core.Models.OpmlImportChange> Preview { get; private set; } = [];
    public event EventHandler? Applied;
    public bool CanApply => !IsLoading && _plan is not null && Error.Length == 0;
    public bool HasError => Error.Length != 0;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Filename { get; private set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Summary { get; private set; } = Localization.Localizer.Current.Message("OpmlChooseFileHint");

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Error { get; private set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool ReplaceOldStations { get; set; }

    partial void OnReplaceOldStationsChanged(bool value)
    {
        Summary = value ? Localization.Localizer.Current.Message("PrepareReplace") : Localization.Localizer.Current.Message("PrepareMerge");
        RefreshPreview();
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsLoading { get; private set; }

    public async Task LoadAsync(Stream stream, string filename)
    {
        IsLoading = true;
        _plan = null;
        Error = "";
        Summary = Localization.Localizer.Current.Message("ReadingOPML");
        NotifyState();
        try
        {
            var stations = await Task.Run(() => Infrastructure.OpmlReader.Read(stream));
            foreach (var item in Entries) item.PropertyChanged -= SelectionChanged;
            Entries.Clear();
            foreach (var station in stations.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Line))
            {
                var item = new OpmlSelection(station);
                item.PropertyChanged += SelectionChanged;
                Entries.Add(item);
            }
            Filename = filename;
            IsLoading = false;
            RefreshPreview();
        }
        catch (Exception exception)
        {
            foreach (var item in Entries) item.PropertyChanged -= SelectionChanged;
            Entries.Clear();
            Filename = filename;
            Preview = [];
            OnPropertyChanged(nameof(Preview));
            Error = Localization.Localizer.Current.Message("OpmlReadFailed", exception.Message);
            Summary = Localization.Localizer.Current.Message("OpmlUnchanged");
        }
        finally
        {
            IsLoading = false;
            NotifyState();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SelectAll()
    {
        SelectEntries(true);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void SelectNone()
    {
        SelectEntries(false);
    }

    private void SelectEntries(bool selected)
    {
        _updatingSelection = true;
        try { foreach (var entry in Entries) entry.IsSelected = selected; }
        finally { _updatingSelection = false; }
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        if (IsLoading) return;
        _plan = null;
        Error = "";
        try
        {
            var selected = Entries.Where(item => item.IsSelected).Select(item => item.Station).OrderBy(s => s.SourceIndex).ToArray();
            _plan = prepare(selected, ReplaceOldStations);
            Preview = _plan.Changes;
            Summary = Localization.Localizer.Current.Message("OpmlPreviewSummary", selected.Length, _plan.Changes.Count(c => c.Action == "Add"), _plan.Changes.Count(c => c.Action == "Update"), _plan.Changes.Count(c => c.Action == "Keep"), _plan.Changes.Count(c => c.Action == "Remove"));
            Error = blockingReason(_plan) ?? "";
        }
        catch (Exception exception)
        {
            Preview = [];
            Summary = Localization.Localizer.Current.Message("OpmlNoChanges");
            Error = exception.Message;
        }
        OnPropertyChanged(nameof(Preview));
        NotifyState();
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        try
        {
            apply(Entries.Where(item => item.IsSelected).Select(item => item.Station).OrderBy(s => s.SourceIndex).ToArray(), ReplaceOldStations);
            Applied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            Error = Localization.Localizer.Current.Message("ImportApply", exception.Message);
            NotifyState();
        }
    }

    private void SelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (!_updatingSelection && args.PropertyName == nameof(OpmlSelection.IsSelected)) RefreshPreview();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(HasError));
        ApplyCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        foreach (var item in Entries) item.PropertyChanged -= SelectionChanged;
    }
}

public sealed partial class OpmlSelection(Core.Models.OpmlStation station) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public Core.Models.OpmlStation Station { get; } = station;
    public string Name => Station.Name;
    public string Url => Station.Url;
    public string Description => Station.Description ?? "";
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsSelected { get; set; } = true;
}
