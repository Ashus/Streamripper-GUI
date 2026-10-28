namespace StreamripperGui.Core.Models;

public static class StationCatalog
{
    public static string DirectoryName(string name)
    {
        const string invalid = "<>:\"/\\|?*";
        var safe = new string(name.Trim().Select(c => c < 32 || invalid.Contains(c) ? '_' : c).ToArray()).TrimEnd('.', ' ');
        if (safe.Length == 0) throw new ArgumentException("Enter a station name containing usable characters.");
        if (System.Text.RegularExpressions.Regex.IsMatch(safe.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            safe = "_" + safe;
        return safe;
    }

    public static Station Edit(Station original, string name, string url, IEnumerable<Station> stations)
    {
        name = name.Trim();
        url = url.Trim();
        if (name.Length == 0) throw new ArgumentException("Enter a station name.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS stream URL.");
        var others = stations.Where(s => s.Id != original.Id).ToArray();
        if (others.Any(s => string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("A station with this name already exists.");
        var requested = name;
        var suffix = 2;
        while (others.Any(s =>
                   string.Equals(DirectoryName(s.Name), DirectoryName(name), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            name = $"{requested} ({suffix++})";
        return original with
        {
            Name = name,
            Url = url,
            ImportedName = string.Equals(original.Name, name, StringComparison.Ordinal) ? original.ImportedName : null,
            LastObservedStream = original.Url == url ? original.LastObservedStream : null
        };
    }

    public static IReadOnlyList<T> Visible<T>(IEnumerable<T> stations, Func<T, string> name,
        Func<T, string> id, Func<T, bool> active, string query) =>
        stations.Where(s => active(s) || name(s).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(active)
            .ThenBy(name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id, StringComparer.Ordinal).ToArray();
}
