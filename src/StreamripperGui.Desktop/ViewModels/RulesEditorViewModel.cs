namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class RulesEditorViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    private readonly Infrastructure.MetadataRuleFiles _files;
    private readonly Func<string> _executable;
    private readonly Action<RuleSetChoice, string> _saveCatalog;
    private readonly Avalonia.Threading.DispatcherTimer _timer;
    private int _version;
    private bool _disposed;
    private bool _loading;
    private RuleSetChoice? _selected;
    private string _savedText = "";
    private string _savedName = "";
    private bool _newRules;
    private bool _acceptSelection;
    private bool _changingSelection;
    public Func<Task<bool>>? ConfirmLeaveDraft { get; set; }

    public RulesEditorViewModel(string configurationDirectory, Func<string> executable, Action<RuleSetChoice, string> saveCatalog)
    {
        _files = new(configurationDirectory);
        _executable = executable;
        _saveCatalog = saveCatalog;
        _timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _timer.Tick += ValidationTick;
    }

    public System.Collections.ObjectModel.ObservableCollection<RuleSetChoice> RuleSets { get; } = [];
    public RuleSetChoice? Selected
    {
        get => _selected;
        set
        {
            if (!_acceptSelection && (IsDirty || _changingSelection || CheckAndApplyCommand.IsRunning))
            {
                if (value != _selected || _newRules) _ = SelectAsync(value);
                return;
            }
            if (!SetProperty(ref _selected, value) && !_newRules) return;
            _newRules = false;
            if (value is null)
            {
                _version++;
                _loading = true;
                Name = Text = _savedText = _savedName = Status = "";
                _loading = false;
                _timer.Stop();
                Diagnostics = [];
                OnPropertyChanged(nameof(Diagnostics));
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
                RefreshDirtyState();
                return;
            }
            _loading = true;
            try
            {
                Name = value.Name;
                Text = _files.Read(value.File);
                _savedText = Text;
                _savedName = Name;
                Status = Localization.Localizer.Current.Message("RulesLoaded");
            }
            catch (Exception exception) { Text = ""; _savedText = ""; Status = Localization.Localizer.Current.Message("RulesReadFailed", exception.Message); }
            finally { _loading = false; }
            RefreshDirtyState();
            ScheduleValidation();
        }
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Name { get; set; } = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Text { get; set; } = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Status { get; private set; } = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string LiteralText { get; set; } = "";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool SuffixOnly { get; set; } = true;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool PrefixOnly { get; set; }
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int CaretOffset { get; set; }
    public IReadOnlyList<Core.Models.RuleDiagnostic> Diagnostics { get; private set; } = [];
    public bool CanEdit => _newRules || Selected is not null;
    public bool IsDirty => CanEdit && (_newRules || Text != _savedText || Name.Trim() != _savedName);
    public bool CanCheckAndApply => IsDirty;
    public string? LastAppliedId { get; private set; }
    public Func<string, bool>? IsRulesFileActive { get; set; }
    public event EventHandler? DiagnosticsChanged;

    partial void OnTextChanged(string value)
    {
        _ = value;
        RefreshDirtyState();
        if (!_loading) ScheduleValidation();
    }

    private void ScheduleValidation()
    {
        _version++;
        // Diagnostics belong to the exact validated draft, never the next edit or template.
        Diagnostics = [];
        OnPropertyChanged(nameof(Diagnostics));
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        _timer.Stop();
        _timer.Start();
    }

    partial void OnNameChanged(string value) => RefreshDirtyState();
    private void RefreshDirtyState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanCheckAndApply));
        CheckAndApplyCommand.NotifyCanExecuteChanged();
    }

    private void ValidationTick(object? sender, EventArgs args)
    {
        _timer.Stop();
        _ = ValidateAsync();
    }

    private async Task<Core.Models.ParsedMetadataRules?> ValidateAsync()
    {
        var version = _version;
        var text = Text;
        var executable = _executable();
        try
        {
            var result = await Task.Run(() =>
            {
                using var validator = new Infrastructure.GlibRegexValidator(executable);
                return Core.Models.MetadataRules.Validate(text, validator);
            });
            if (_disposed || version != _version) return null;
            Diagnostics = result.Diagnostics;
            OnPropertyChanged(nameof(Diagnostics));
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }
        catch (Exception exception)
        {
            if (_disposed || version != _version) return null;
            Diagnostics = [.. Core.Models.MetadataRules.Parse(text).Diagnostics,
                new(0, 0, Localization.Localizer.Current.Message("NativeValidation", exception.Message))];
            OnPropertyChanged(nameof(Diagnostics));
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            return null;
        }
    }

    public async Task<bool> SelectAsync(RuleSetChoice? value)
    {
        if (value == Selected && !_newRules) return true;
        if (_changingSelection || CheckAndApplyCommand.IsRunning) return false;
        _changingSelection = true;
        try
        {
            if (IsDirty && (ConfirmLeaveDraft is null || !await ConfirmLeaveDraft())) return false;
            _acceptSelection = true;
            Selected = value;
            return true;
        }
        finally
        {
            _acceptSelection = false;
            _changingSelection = false;
            OnPropertyChanged(nameof(Selected));
        }
    }

    internal void SelectAfterDeletion(RuleSetChoice? value)
    {
        _acceptSelection = true;
        try { Selected = value; }
        finally { _acceptSelection = false; }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private async Task NewRulesAsync() => await NewDraftAsync();

    public async Task<bool> NewDraftAsync()
    {
        if (_changingSelection || CheckAndApplyCommand.IsRunning) return false;
        _changingSelection = true;
        try
        {
            if (IsDirty && (ConfirmLeaveDraft is null || !await ConfirmLeaveDraft())) return false;
            _acceptSelection = true;
            Selected = null;
            _newRules = true;
            Name = Localization.Localizer.Current.Format("NewFilter");
            Text = Core.Models.MetadataRules.Template;
            ScheduleValidation();
            _savedText = "";
            RefreshDirtyState();
            Status = Localization.Localizer.Current.Message("NewFilterHint");
            return true;
        }
        finally
        {
            _acceptSelection = false;
            _changingSelection = false;
            OnPropertyChanged(nameof(Selected));
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanCheckAndApply))]
    private async Task CheckAndApplyAsync()
    {
        LastAppliedId = null;
        _timer.Stop();
        var text = Text;
        var name = Name.Trim();
        var selected = Selected;
        if (name.Length == 0) { Status = Localization.Localizer.Current.Message("FilterNameRequired"); return; }
        if (RuleSets.Any(r => r != Selected && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
        { Status = Localization.Localizer.Current.Message("FilterNameExists"); return; }
        var result = await ValidateAsync();
        if (result is null || text != Text || name != Name.Trim() || selected != Selected || result.Diagnostics.Any(d => !d.IsWarning))
        { Status = Localization.Localizer.Current.Message("RulesInvalid"); return; }
        try
        {
            var choice = Selected ?? new RuleSetChoice(Guid.NewGuid().ToString("N"), name, "");
            if (choice.File.Length == 0) choice = new(choice.Id, name, $"rules/{choice.Id}.txt");
            if (Selected is not null) _saveCatalog(choice, name);
            _files.Write(choice.File, text);
            if (Selected is null) _saveCatalog(choice, name);
            if (!RuleSets.Any(r => r.Id == choice.Id)) RuleSets.Add(choice);
            choice.Name = name;
            _selected = choice;
            CatalogOrder.Sort(RuleSets, rule => rule.Id, rule => rule.Name);
            OnPropertyChanged(nameof(Selected));
            _savedText = text;
            _savedName = name;
            _newRules = false;
            LastAppliedId = choice.Id;
            RefreshDirtyState();
            var localizer = Localization.Localizer.Current;
            Status = localizer.Message(IsRulesFileActive?.Invoke(_files.Resolve(choice.File)) == true ? "RulesSavedActive" : "RulesSavedInactive");
        }
        catch (Exception exception) { Status = Localization.Localizer.Current.Message("RulesSaveFailed", exception.Message); }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void InsertRemoval() => Insert(false);
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void InsertExclusion() => Insert(true);
    private void Insert(bool exclude)
    {
        try
        {
            var rule = Core.Models.MetadataRules.LiteralRule(LiteralText, exclude, SuffixOnly, PrefixOnly);
            var insertion = Core.Models.MetadataRules.InsertGenerated(Text, rule);
            Text = insertion.Text;
            CaretOffset = insertion.Caret;
        }
        catch (Exception exception) { Status = Localization.Localizer.Current.Message("RulesReadFailed", exception.Message); }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= ValidationTick;
    }
}

public sealed class RuleSetChoice(string? id, string name, string file) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string? Id { get; } = id;
    public string File { get; } = file;
    private string _name = name;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public override string ToString() => Name;
}
