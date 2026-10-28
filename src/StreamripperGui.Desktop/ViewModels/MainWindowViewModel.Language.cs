namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<OptionChoice> Languages { get; } =
        [new("auto", Localization.Localizer.Current.Message("Automatic")), new("en", Localization.Localizer.Current.Message("English")), new("cs", Localization.Localizer.Current.Message("Czech"))];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedLanguage))]
    public partial string LanguageMode { get; set; } = "auto";

    public OptionChoice? SelectedLanguage
    {
        get => Languages.FirstOrDefault(l => l.Value == LanguageMode);
        set { if (value is not null) LanguageMode = value.Value; }
    }

    private void InitializeLocalization()
    {
        LanguageMode = _configuration["application"]?["language"]?.GetValue<string>() ?? "auto";
        Localization.Localizer.Current.SetMode(LanguageMode);
        Localization.Localizer.Current.LanguageChanged += LanguageChanged;
        LanguageChanged(this, EventArgs.Empty);
    }

    partial void OnLanguageModeChanged(string value)
    {
        Localization.Localizer.Current.SetMode(value);
        SaveApplicationChoice("language", value);
    }

    private void LanguageChanged(object? sender, EventArgs args)
    {
        ProfileChoices[0].Name = Localization.Localizer.Current.Format("Default");
        RuleChoices[0].Name = Localization.Localizer.Current.Format("None");
        if (SelectedProfile is { Id: null }) ProfileNameDraft = ProfileChoices[0].Name;
        foreach (var station in _stations) station.RefreshTranslations();
    }
}
