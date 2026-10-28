namespace StreamripperGui.Core.Tests;

public sealed partial class LocalizationTests
{
    private readonly Desktop.Localization.TranslationCatalog _catalog = new();

    [Xunit.Fact]
    public void CatalogsHaveMatchingSemanticKeysAndFormatArguments()
    {
        var english = Read("en");
        var czech = Read("cs");
        Xunit.Assert.Equal(english.Keys.Order(), czech.Keys.Order());
        Xunit.Assert.Equal(english.Count, english.Values.Distinct().Count());
        foreach (var (key, value) in english)
        {
            Xunit.Assert.Matches("^[A-Z][A-Za-z0-9]*$", key);
            Xunit.Assert.DoesNotMatch("^K[0-9a-f]{12}$", key);
            Xunit.Assert.False(string.IsNullOrWhiteSpace(czech[key]));
            var enFormat = System.Text.CompositeFormat.Parse(value);
            var csFormat = System.Text.CompositeFormat.Parse(czech[key]);
            Xunit.Assert.Equal(enFormat.MinimumArgumentCount, csFormat.MinimumArgumentCount);
            Xunit.Assert.Equal(Placeholders(value), Placeholders(czech[key]));
        }
    }

    [Xunit.Fact]
    public void ExistingMessagesCanBeRenderedAgainAfterLanguageChanges()
    {
        var message = _catalog.Message("StationsLoaded", 46);
        Xunit.Assert.Equal("Loaded 46 stations.", _catalog.Translate(message, "en"));
        Xunit.Assert.Equal("Načteno stanic: 46.", _catalog.Translate(message, "cs"));
        Xunit.Assert.Equal(message, _catalog.Translate(message, "en"));
        Xunit.Assert.Equal("Stations", _catalog.Get("Stations", "de"));
        Xunit.Assert.Equal("UnknownKey", _catalog.Get("UnknownKey", "cs"));
    }

    [Xunit.Fact]
    public void MessageArgumentsAndNativeOutputKeepTheirOriginalText()
    {
        const string name = "Ready / Žluťoučké rádio (2)";
        Xunit.Assert.Equal("Konzole · " + name, _catalog.Translate(_catalog.Message("ConsoleStationTitle", name), "cs"));
        Xunit.Assert.Equal("Žluťoučké rádio", _catalog.Translate("Žluťoučké rádio", "cs"));
        const string native = "[ripping... ] Ready - Stop [ 00:13]";
        Xunit.Assert.Equal(native, _catalog.Translate(native, "cs"));
        Xunit.Assert.Equal("Formát: MP3", _catalog.Format("cs", "Format", "MP3"));
    }

    [Xunit.Fact]
    public void NestedApplicationAndDomainErrorsAreTranslated()
    {
        var message = _catalog.Message("StationSaveFailed", "Enter a station name.");
        Xunit.Assert.Equal("Stanice nebyla uložena: Zadej název stanice.", _catalog.Translate(message, "cs"));
        var opml = _catalog.Message("OpmlReadFailed", "Line 7, 'My station': Enter an absolute HTTP or HTTPS stream URL.");
        Xunit.Assert.Contains("Řádek 7, 'My station': Zadej úplnou HTTP nebo HTTPS adresu streamu.", _catalog.Translate(opml, "cs"));
        Xunit.Assert.Equal("Časový limit musí být mezi 1 a 86400.", _catalog.Translate("Timeout must be between 1 and 86400.", "cs"));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\{\d+\}")]
    private static partial System.Text.RegularExpressions.Regex PlaceholderPattern();

    private static string[] Placeholders(string text) => PlaceholderPattern().Matches(text).Select(m => m.Value).Order().ToArray();

    private static Dictionary<string, string> Read(string language)
    {
        using var stream = typeof(LocalizationTests).Assembly.GetManifestResourceStream("Translations." + language + ".json")!;
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
