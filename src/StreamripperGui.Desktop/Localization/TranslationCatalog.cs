namespace StreamripperGui.Desktop.Localization;

// UI-independent catalog; also compiled into tests without loading Avalonia.
internal sealed class TranslationCatalog
{
    private readonly Dictionary<string, string> _en;
    private readonly Dictionary<string, string> _cs;
    private readonly List<(System.Text.RegularExpressions.Regex Pattern, string En, string Cs)> _templates = [];
    public string Get(string key, string language) =>
        (language == "cs" ? _cs.GetValueOrDefault(key) : null) ?? _en.GetValueOrDefault(key) ?? key;

    public TranslationCatalog()
    {
        _en = Load("en");
        _cs = Load("cs");
        foreach (var (key, value) in _en)
        {
            if (!value.Contains("{0}", StringComparison.Ordinal)) continue;
            var czech = _cs.GetValueOrDefault(key) ?? value;
            var escaped = System.Text.RegularExpressions.Regex.Escape(value);
            escaped = System.Text.RegularExpressions.Regex.Replace(escaped, @"\\\{(\d+)\}", match => $"(?<v{match.Groups[1].Value}>.*?)");
            _templates.Add((new System.Text.RegularExpressions.Regex("^" + escaped + "$",
                System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromMilliseconds(50)), value, czech));
        }
    }

    private static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(TranslationCatalog).Assembly.GetManifestResourceStream("Translations." + language + ".json")
            ?? throw new InvalidOperationException("Missing translations: " + language);
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    // View-model messages keep an English identity so an existing message can be
    // rendered again when the language changes. Arguments retain their original text.
    public string Message(string key, params object?[] arguments) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, _en.GetValueOrDefault(key) ?? key, arguments);

    public string Format(string language, string key, params object?[] arguments) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, Get(key, language), arguments);

    public string Translate(string text, string language) => Translate(text, language, 0);

    private string Translate(string text, string language, int depth)
    {
        foreach (var (key, english) in _en)
            if (english == text) return Get(key, language);
        foreach (var (pattern, english, czech) in _templates)
        {
            var match = pattern.Match(text);
            if (!match.Success) continue;
            var values = Enumerable.Range(0, 12).Select<int, object>(index => match.Groups["v" + index].Value).ToArray();
            if (depth < 4 && (english.EndsWith(": {0}", StringComparison.Ordinal) || english.Contains("unchanged: {0}", StringComparison.Ordinal) ||
                english.Contains("files: {0}", StringComparison.Ordinal) || english == "{0} must be between {1} and {2}."))
                values[0] = Translate((string)values[0], language, depth + 1);
            if (depth < 4 && english == "Line {0}, '{1}': {2}")
                values[2] = Translate((string)values[2], language, depth + 1);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, language == "cs" ? czech : english, values);
        }
        return text;
    }
}
