namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void DiscoverRules()
    {
        if (_loadFailed || Rules is null || Rules.CheckAndApplyCommand.IsRunning) return;
        try
        {
            var files = new Infrastructure.MetadataRuleFiles(_store.DirectoryPath);
            var registered = RuleChoices.Where(rule => rule.Id is not null).Select(rule => rule.File);
            var names = RuleChoices.Select(rule => rule.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added = files.DiscoverNew(registered).Select(file => new RuleSetChoice(Guid.NewGuid().ToString("N"),
                UniqueRuleName(Path.GetFileNameWithoutExtension(file), names), file)).ToArray();
            if (added.Length != 0) RegisterRules(added);
        }
        catch (Exception exception) { Message = Localization.Localizer.Current.Message("RulesDiscoveryFailed", exception.Message); }
    }

    private static string UniqueRuleName(string requested, HashSet<string> names)
    {
        var stem = string.IsNullOrWhiteSpace(requested) ? "Filter" : requested.Trim();
        var name = stem;
        for (var suffix = 2; !names.Add(name); suffix++) name = $"{stem} ({suffix})";
        return name;
    }

    public RulesImportViewModel CreateRulesImport(string sourceDirectory)
    {
        sourceDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        var names = RuleChoices.Select(rule => rule.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(file => string.Equals(Path.GetExtension(file), ".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
            .Select(file => new RulesImportEntry(file, UniqueRuleName(Path.GetFileNameWithoutExtension(file), names))).ToArray();
        return new RulesImportViewModel(entries, selected => ImportRules(sourceDirectory, selected));
    }

    private void ImportRules(string sourceDirectory, IReadOnlyList<RulesImportEntry> entries)
    {
        if (_loadFailed || Rules.CheckAndApplyCommand.IsRunning) throw new InvalidOperationException(Localization.Localizer.Current.Message("RulesImportBusy"));
        if (entries.Count == 0) throw new ArgumentException(Localization.Localizer.Current.Message("RulesImportSelectionRequired"));
        var files = new Infrastructure.MetadataRuleFiles(_store.DirectoryPath);
        var names = RuleChoices.Select(rule => rule.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<(RuleSetChoice Rule, byte[] Content)>();
        using var validator = new Infrastructure.GlibRegexValidator(Path.GetFullPath(ExecutablePath, AppContext.BaseDirectory));
        foreach (var entry in entries)
        {
            var name = entry.Name.Trim();
            if (name.Length == 0 || !names.Add(name)) throw new InvalidDataException(Localization.Localizer.Current.Message("RulesImportNameError", entry.Name));
            var source = Path.GetFullPath(entry.File);
            if (!string.Equals(Path.GetDirectoryName(source), sourceDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(source), ".txt", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Localization.Localizer.Current.Message("RulesImportFileChanged", entry.File));
            var bytes = File.ReadAllBytes(source);
            var text = new System.Text.UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            var errors = Core.Models.MetadataRules.Validate(text, validator).Diagnostics.Where(diagnostic => !diagnostic.IsWarning).ToArray();
            if (errors.Length != 0) throw new InvalidDataException(entry.File + ": " + string.Join("; ", errors));
            // New identities prevent overwriting existing files, including files used by running sessions.
            var id = Guid.NewGuid().ToString("N");
            prepared.Add((new(id, name, $"rules/{id}.txt"), bytes));
        }
        var created = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.Combine(_store.DirectoryPath, "rules"));
            foreach (var (rule, content) in prepared)
            {
                var path = files.Resolve(rule.File);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created.Add(path);
                stream.Write(content);
                stream.Flush(true);
            }
            RegisterRules(prepared.Select(item => item.Rule).ToArray());
        }
        catch
        {
            foreach (var path in created)
            {
                try { File.Delete(path); }
                catch (IOException exception) { System.Diagnostics.Trace.TraceWarning($"Cleaning up an imported file failed: {exception.Message}"); }
            }
            throw;
        }
    }

    private void RegisterRules(IReadOnlyList<RuleSetChoice> added)
    {
        var copy = _configuration.DeepClone().AsObject();
        var catalog = copy["metadataRuleSets"]!.AsArray();
        foreach (var rule in added) catalog.Add(new System.Text.Json.Nodes.JsonObject { ["id"] = rule.Id, ["name"] = rule.Name, ["file"] = rule.File });
        _store.SaveConfiguration(copy);
        _configuration = copy;
        foreach (var rule in added) { RuleChoices.Add(rule); Rules.RuleSets.Add(rule); }
        CatalogOrder.Sort(RuleChoices, rule => rule.Id, rule => rule.Name);
        CatalogOrder.Sort(Rules.RuleSets, rule => rule.Id, rule => rule.Name);
        RefreshRuleCommands();
    }
}
