namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class RulesImportViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private readonly Action<IReadOnlyList<RulesImportEntry>> _import;
    public RulesImportViewModel(IReadOnlyList<RulesImportEntry> entries, Action<IReadOnlyList<RulesImportEntry>> import)
    {
        Entries = entries;
        _import = import;
        foreach (var entry in Entries) entry.PropertyChanged += EntryChanged;
    }
    public IReadOnlyList<RulesImportEntry> Entries { get; }
    public bool HasEntries => Entries.Count != 0;
    public bool CanImport => Entries.Any(entry => entry.IsSelected);
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Error { get; private set; } = "";
    public event EventHandler? Applied;
    private void EntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RulesImportEntry.IsSelected)) ImportCommand.NotifyCanExecuteChanged();
    }
    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanImport))]
    private void Import()
    {
        Error = "";
        try
        {
            _import(Entries.Where(entry => entry.IsSelected).ToArray());
            Applied?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception) { Error = exception.Message; }
    }
    public void Dispose()
    {
        foreach (var entry in Entries) entry.PropertyChanged -= EntryChanged;
    }
}

public sealed partial class RulesImportEntry(string file, string name) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string File { get; } = file;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Name { get; set; } = name;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool IsSelected { get; set; } = true;
}
