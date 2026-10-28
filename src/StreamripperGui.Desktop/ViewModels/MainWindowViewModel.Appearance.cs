namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public IReadOnlyList<OptionChoice> Themes { get; } = [new("auto", Localization.Localizer.Current.Message("Automatic")), new("light", Localization.Localizer.Current.Message("Light")), new("dark", Localization.Localizer.Current.Message("Dark"))];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedTheme))]
    public partial string ThemeMode { get; set; } = "auto";

    public OptionChoice? SelectedTheme
    {
        get => Themes.FirstOrDefault(t => t.Value == ThemeMode);
        set { if (value is not null) ThemeMode = value.Value; }
    }

    private void InitializeAppearance()
    {
        var saved = _configuration["application"]?["theme"]?.GetValue<string>();
        ThemeMode = saved is "light" or "dark" ? saved : "auto";
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = ThemeMode switch
            {
                "light" => Avalonia.Styling.ThemeVariant.Light,
                "dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Default
            };
    }

    partial void OnThemeModeChanged(string value)
    {
        _ = value;
        ApplyTheme();
        SaveApplicationChoice("theme", value);
    }
}
