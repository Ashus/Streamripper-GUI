namespace StreamripperGui.Core.Models;

public sealed record OpmlStation(string Name, string Url, string? Description = null, string? Category = null, int Line = 0, int SourceIndex = 0);

public sealed record OpmlImportChange(string Action, string Name, string? PreviousUrl, string? Url, string? Note)
{
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public bool HasUrl => !string.IsNullOrEmpty(Url);
    public bool HasPreviousUrl => !string.IsNullOrEmpty(PreviousUrl) && PreviousUrl != Url;
}

public sealed record OpmlImportPlan(IReadOnlyList<Station> Stations, IReadOnlyList<OpmlImportChange> Changes);

public static class OpmlImportPlanner
{
    public static OpmlImportPlan Prepare(IReadOnlyList<Station> existing, IReadOnlyList<OpmlStation> imported, bool replace)
    {
        if (imported.Count == 0) throw new ArgumentException("Select at least one station to import.");
        // Collapse duplicates before applying: the last selected occurrence wins, including its original spelling.
        var entries = new Dictionary<string, OpmlStation>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in imported)
        {
            var checkedStation = StationCatalog.Edit(new(), entry.Name, entry.Url, []);
            entries[checkedStation.Name] = entry with { Name = checkedStation.Name, Url = checkedStation.Url };
        }
        var exact = existing.ToDictionary(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase);
        var aliases = existing.Where(s => s.ImportedName is not null)
            .GroupBy(s => s.ImportedName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var matches = new List<(OpmlStation Entry, Station? Station)>();
        var matchedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.Values)
        {
            exact.TryGetValue(entry.Name, out var station);
            // An automatically suffixed import keeps its original name for repeat imports.
            // Manual renaming clears this alias; exact display-name matching always takes priority.
            if (station is null && aliases.TryGetValue(entry.Name, out var candidates) && candidates.Length == 1)
                station = candidates[0];
            if (station is not null && !matchedIds.Add(station.Id))
                throw new ArgumentException($"Imported names ambiguously refer to station '{station.Name}'.");
            matches.Add((entry, station));
        }
        var result = replace ? existing.Where(s => matchedIds.Contains(s.Id)).ToList() : existing.ToList();
        var changes = new List<OpmlImportChange>();
        foreach (var (entry, original) in matches)
        {
            var source = original ?? new Station();
            var targetName = original?.ImportedName is not null &&
                             !string.Equals(original.Name, entry.Name, StringComparison.OrdinalIgnoreCase)
                ? original.Name : entry.Name;
            var updated = StationCatalog.Edit(source, targetName, entry.Url, result);
            if (!string.Equals(updated.Name, entry.Name, StringComparison.Ordinal))
                updated = updated with { ImportedName = entry.Name };
            var index = result.FindIndex(s => s.Id == source.Id);
            if (index < 0) result.Add(updated);
            else result[index] = updated;
            var changed = original is null || original.Name != updated.Name || original.Url != updated.Url;
            var action = original is null ? "Add" : changed ? "Update" : "Keep";
            var notes = new List<string>();
            if (updated.Name != entry.Name) notes.Add($"Imported as '{updated.Name}' to keep names and recording folders unique");
            changes.Add(new(action, updated.Name, original?.Url, updated.Url, notes.Count == 0 ? null : string.Join(" · ", notes)));
        }
        if (replace)
            changes.AddRange(existing.Where(s => !matchedIds.Contains(s.Id))
                .Select(s => new OpmlImportChange("Remove", s.Name, s.Url, null, "Recorded files and profiles/rules are preserved")));
        return new(result, changes);
    }
}
