namespace StreamripperGui.Infrastructure;

/// <summary>Versioned files are replaced individually; station edits never rewrite shared profiles.</summary>
public sealed class JsonConfigurationStore(string directory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
    };

    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public bool IsPortable { get; } = File.Exists(Path.Combine(directory, "portable.flag"));

    public string PathForStorage(string path, string executableDirectory)
    {
        var absolute = Path.GetFullPath(path.Trim(), executableDirectory);
        if (!IsPortable) return absolute;
        var relative = Path.GetRelativePath(executableDirectory, absolute);
        if (Path.IsPathRooted(relative))
            throw new ArgumentException("In portable mode, choose a path on the same drive as the GUI so it can be saved relatively.");
        return relative;
    }

    public static string DefaultDirectory(string executableDirectory) =>
        File.Exists(Path.Combine(executableDirectory, "portable.flag"))
            ? executableDirectory
            : PlatformPaths.ConfigurationDirectory;

    public System.Text.Json.Nodes.JsonObject LoadConfiguration()
    {
        var path = Path.Combine(DirectoryPath, "config.json");
        if (!File.Exists(path)) return CreateDefaultConfiguration();
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) as System.Text.Json.Nodes.JsonObject
            ?? throw new InvalidDataException("config.json must contain an object.");
        CheckVersion(document["schemaVersion"]?.GetValue<int>());
        if (document["application"] is not System.Text.Json.Nodes.JsonObject ||
            document["recording"] is not System.Text.Json.Nodes.JsonObject recording ||
            recording["defaults"] is not System.Text.Json.Nodes.JsonObject ||
            recording["profiles"] is not System.Text.Json.Nodes.JsonArray ||
            document["metadataRuleSets"] is not System.Text.Json.Nodes.JsonArray)
            throw new InvalidDataException("config.json has an invalid configuration structure.");
        return document;
    }

    public StationFile LoadStations(System.Text.Json.Nodes.JsonObject configuration)
    {
        var path = Path.Combine(DirectoryPath, "stations.json");
        if (!File.Exists(path)) return new StationFile();
        var file = System.Text.Json.JsonSerializer.Deserialize<StationFile>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("stations.json must contain an object.");
        CheckVersion(file.SchemaVersion);
        file.Stations = file.Stations.Select(CleanLegacyStationFields).ToList();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Core.Models.Station? station in file.Stations)
        {
            if (station is null || string.IsNullOrWhiteSpace(station.Id) || !ids.Add(station.Id))
                throw new InvalidDataException("Station IDs must be present and unique.");
            var validated = Core.Models.StationCatalog.Edit(station, station.Name, station.Url, []);
            if (validated.Name != station.Name || validated.Url != station.Url ||
                !names.Add(station.Name) || !directories.Add(Core.Models.StationCatalog.DirectoryName(station.Name)))
                throw new InvalidDataException("Station names and recording directories must be unique and trimmed.");
            ValidateReference(configuration, station.RecordingProfileId, "recording", "profiles");
            ValidateReference(configuration, station.MetadataRuleSetId, "metadataRuleSets");
        }
        return file;
    }

    private static void ValidateReference(System.Text.Json.Nodes.JsonObject configuration, string? id, params string[] path)
    {
        if (id is null) return;
        System.Text.Json.Nodes.JsonNode? node = configuration;
        foreach (var key in path) node = node?[key];
        if (node is not System.Text.Json.Nodes.JsonArray array ||
            !array.Any(entry => entry?["id"]?.GetValue<string>() == id))
            throw new InvalidDataException($"Configuration reference '{id}' does not exist.");
    }

    public void SaveConfiguration(System.Text.Json.Nodes.JsonObject configuration)
    {
        CheckVersion(configuration["schemaVersion"]?.GetValue<int>());
        WriteAtomic("config.json", configuration.ToJsonString(Options));
    }

    public void SaveStations(StationFile stations)
    {
        CheckVersion(stations.SchemaVersion);
        var cleaned = new StationFile { Stations = stations.Stations.Select(CleanLegacyStationFields).ToList(), AdditionalData = stations.AdditionalData };
        WriteAtomic("stations.json", System.Text.Json.JsonSerializer.Serialize(cleaned, Options));
    }

    private void WriteAtomic(string filename, string contents)
    {
        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, filename);
        var temporary = Path.Combine(DirectoryPath, $".{filename}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(contents + Environment.NewLine);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak");
            else File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static Core.Models.Station CleanLegacyStationFields(Core.Models.Station station)
    {
        if (station is null) throw new InvalidDataException("Station must be an object.");
        var additional = station.AdditionalData?.Where(pair => pair.Key is not ("description" or "importedCategory"))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var observation = station.LastObservedStream?.DeepClone().AsObject();
        observation?.Remove("url");
        return station with { AdditionalData = additional, LastObservedStream = observation };
    }

    private static void CheckVersion(int? version)
    {
        if (version != 1) throw new InvalidDataException($"Unsupported configuration schema version: {version}.");
    }

    public static System.Text.Json.Nodes.JsonObject CreateDefaultConfiguration()
    {
        var configuration = System.Text.Json.Nodes.JsonNode.Parse("""
        {
          "schemaVersion": 1,
          "application": { "language": "auto", "theme": "auto", "streamripperPath": "streamripper/streamripper.exe" },
          "recording": {
            "directory": "",
            "defaults": {
              "filenamePattern": "%q - %A - %T", "timeoutSeconds": 8,
              "keepIncomplete": true, "metadataLog": true, "incompleteMinDurationSeconds": 900
            },
            "profiles": []
          },
          "metadataRuleSets": []
        }
        """)!.AsObject();
        configuration["application"]!["streamripperPath"] = PlatformPaths.StreamripperExecutable;
        return configuration;
    }
}

public sealed class StationFile
{
    [System.Text.Json.Serialization.JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    [System.Text.Json.Serialization.JsonRequired]
    public List<Core.Models.Station> Stations { get; set; } = [];

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; init; }
}
