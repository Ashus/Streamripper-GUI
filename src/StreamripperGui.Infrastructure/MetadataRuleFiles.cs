namespace StreamripperGui.Infrastructure;

public sealed class MetadataRuleFiles(string configurationDirectory)
{
    public IReadOnlyList<string> DiscoverNew(IEnumerable<string> registeredFiles)
    {
        var root = Path.GetFullPath(configurationDirectory);
        var directory = Path.Combine(root, "rules");
        if (!Directory.Exists(directory)) return [];
        var registered = registeredFiles.Select(Resolve).ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase) && !registered.Contains(Path.GetFullPath(path)))
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).ToArray();
    }
    public string Resolve(string relativeFile)
    {
        var root = Path.GetFullPath(Path.Combine(configurationDirectory, "rules"));
        var path = Path.GetFullPath(relativeFile, Path.GetFullPath(configurationDirectory));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("The rules file must be inside the active configuration's rules directory.");
        return path;
    }

    public string Read(string relativeFile) => File.ReadAllText(Resolve(relativeFile), new System.Text.UTF8Encoding(false, true));

    public void Write(string relativeFile, string content)
    {
        var path = Resolve(relativeFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(new System.Text.UTF8Encoding(false, true).GetBytes(content));
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
