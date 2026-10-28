namespace StreamripperGui.Desktop.Localization;

public sealed class Localizer : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public static Localizer Current { get; } = new();
    private readonly TranslationCatalog _catalog = new();
    private string _language = "en";
    public event EventHandler? LanguageChanged;
    public string Language => _language;
    public string this[string key] => _catalog.Get(key, _language);

    private Localizer() { }

    public void SetMode(string mode)
    {
        var language = ResolveLanguage(mode, System.Globalization.CultureInfo.CurrentUICulture);
        if (_language == language) return;
        _language = language;
        OnPropertyChanged(nameof(Language));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static string ResolveLanguage(string mode, System.Globalization.CultureInfo culture) =>
        mode is "en" or "cs" ? mode : culture.TwoLetterISOLanguageName == "cs" ? "cs" : "en";

    public string Message(string key, params object?[] arguments) => _catalog.Message(key, arguments);
    public string Format(string key, params object?[] arguments) => _catalog.Format(_language, key, arguments);
    public string Translate(string text) => _catalog.Translate(text, _language);
}

public sealed class T(string key) : Avalonia.Markup.Xaml.MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Avalonia.Data.MultiBinding
        {
            Converter = new KeyConverter(key),
            Bindings = { new Avalonia.Data.Binding(nameof(Localizer.Language)) { Source = Localizer.Current } }
        };

    private sealed class KeyConverter(string resourceKey) : Avalonia.Data.Converters.IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            Localizer.Current[resourceKey];
    }
}

public sealed class TranslateBinding(string path) : Avalonia.Markup.Xaml.MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Avalonia.Data.MultiBinding
        {
            Converter = TextConverter.Instance,
            Bindings =
            {
                new Avalonia.Data.Binding(path),
                new Avalonia.Data.Binding(nameof(Localizer.Language)) { Source = Localizer.Current }
            }
        };
}

internal sealed class TextConverter : Avalonia.Data.Converters.IMultiValueConverter
{
    public static TextConverter Instance { get; } = new();
    public object Convert(IList<object?> values, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        values.Count == 0 || values[0] is null || values[0] == Avalonia.AvaloniaProperty.UnsetValue
            ? "" : Localizer.Current.Translate(values[0]!.ToString() ?? "");
}
