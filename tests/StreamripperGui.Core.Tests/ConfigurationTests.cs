namespace StreamripperGui.Core.Tests;

public sealed class ConfigurationTests
{
    [Xunit.Fact]
    public void StationEditPreservesReferencesAndInvalidatesObservationOnlyForChangedUrl()
    {
        var observation = new System.Text.Json.Nodes.JsonObject { ["audioFormat"] = "MP3" };
        var original = new Models.Station
        {
            Name = "Jazz", Url = "https://example.com/a?x=1&y=2",
            RecordingProfileId = "shared", MetadataRuleSetId = "hotmix", LastObservedStream = observation
        };
        var renamed = Models.StationCatalog.Edit(original, " Jazz 2 ", original.Url, []);
        Xunit.Assert.Equal(original.Id, renamed.Id);
        Xunit.Assert.Equal("shared", renamed.RecordingProfileId);
        Xunit.Assert.Equal("hotmix", renamed.MetadataRuleSetId);
        Xunit.Assert.Same(observation, renamed.LastObservedStream);
        Xunit.Assert.Null(Models.StationCatalog.Edit(renamed, renamed.Name, "https://example.com/b", []).LastObservedStream);
    }

    [Xunit.Fact]
    public void NamesAreUniqueAndSanitizedDirectoryCollisionsReceiveStableSuffixes()
    {
        var first = new Models.Station { Name = "Radio: Jazz", Url = "https://example.com/a" };
        var second = Models.StationCatalog.Edit(new(), "Radio? Jazz", first.Url, [first]);
        Xunit.Assert.Equal("Radio? Jazz (2)", second.Name);
        var third = Models.StationCatalog.Edit(new(), "Radio? Jazz", first.Url, [first, second]);
        Xunit.Assert.Equal("Radio? Jazz (3)", third.Name);
        Xunit.Assert.Throws<ArgumentException>(() => Models.StationCatalog.Edit(new(), " radio: jazz ", first.Url, [first]));
        Xunit.Assert.Equal("_CON", Models.StationCatalog.DirectoryName("CON"));
        Xunit.Assert.Equal("Radio_ a", Models.StationCatalog.DirectoryName("Radio/ a... "));
    }

    [Xunit.Fact]
    public void SearchKeepsActiveStationsAndSortsEachGroup()
    {
        var stations = new[] { ("1", "Zulu", true), ("2", "Alpha", false), ("3", "Beta", true), ("4", "Jazz", false) };
        var visible = Models.StationCatalog.Visible(stations, s => s.Item2, s => s.Item1, s => s.Item3, "jAz");
        Xunit.Assert.Equal(["Beta", "Zulu", "Jazz"], visible.Select(s => s.Item2).ToArray());
    }

    [Xunit.Theory]
    [Xunit.InlineData("file:///c:/test")]
    [Xunit.InlineData("javascript:alert(1)")]
    [Xunit.InlineData("not a URL")]
    public void InvalidStreamUrlsAreRejected(string url) =>
        Xunit.Assert.Throws<ArgumentException>(() => Models.StationCatalog.Edit(new(), "Radio", url, []));

    [Xunit.Fact]
    public void JsonRoundTripPreservesSharedCatalogsUnknownFieldsAndStationAssignments()
    {
        WithStore((store, directory) =>
        {
            var configuration = Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration();
            configuration["recording"]!["profiles"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "profile", ["options"] = new System.Text.Json.Nodes.JsonObject { ["futureOption"] = true } });
            configuration["metadataRuleSets"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["id"] = "rules", ["file"] = "rules/shared.txt" });
            configuration["futureApplicationField"] = 42;
            store.SaveConfiguration(configuration);
            var station = new Models.Station { Name = "Radio", Url = "https://example.com/radio", RecordingProfileId = "profile", MetadataRuleSetId = "rules" };
            store.SaveStations(new() { Stations = [station] });
            var loaded = store.LoadConfiguration();
            var loadedStation = Xunit.Assert.Single(store.LoadStations(loaded).Stations);
            Xunit.Assert.Equal(station.Id, loadedStation.Id);
            Xunit.Assert.Equal("rules", loadedStation.MetadataRuleSetId);
            store.SaveStations(new());
            var retained = store.LoadConfiguration();
            Xunit.Assert.Equal(42, retained["futureApplicationField"]!.GetValue<int>());
            Xunit.Assert.Single(retained["recording"]!["profiles"]!.AsArray());
            Xunit.Assert.Single(retained["metadataRuleSets"]!.AsArray());
            Xunit.Assert.True(File.Exists(Path.Combine(directory, "stations.json.bak")));
            Xunit.Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        });
    }

    [Xunit.Theory]
    [Xunit.InlineData("{broken")]
    [Xunit.InlineData("{}")]
    [Xunit.InlineData("{\"schemaVersion\":2,\"stations\":[]}")]
    [Xunit.InlineData("{\"schemaVersion\":1,\"stations\":[{\"name\":\"Radio\",\"url\":\"https://example.com\"}]}")]
    public void CorruptIncompleteAndFutureFilesAreRejectedWithoutOverwriting(string json)
    {
        WithStore((store, directory) =>
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "stations.json");
            File.WriteAllText(path, json);
            Xunit.Assert.ThrowsAny<Exception>(() => store.LoadStations(Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration()));
            Xunit.Assert.Equal(json, File.ReadAllText(path));
        });
    }

    [Xunit.Fact]
    public void DanglingReferencesAreErrors()
    {
        WithStore((store, _) =>
        {
            store.SaveStations(new() { Stations = [new() { Name = "Radio", Url = "https://example.com/radio", RecordingProfileId = "missing" }] });
            Xunit.Assert.Throws<InvalidDataException>(() => store.LoadStations(Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration()));
        });
    }

    [Xunit.Fact]
    public void PortableMarkerSelectsConfigurationDirectory()
    {
        WithStore((_, directory) =>
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "portable.flag"), "");
            var expected = OperatingSystem.IsWindows() ? Path.Combine(directory, "config") : directory;
            Xunit.Assert.Equal(expected, Infrastructure.JsonConfigurationStore.DefaultDirectory(directory));
        });
    }

    [Xunit.Theory]
    [Xunit.InlineData(true)]
    [Xunit.InlineData(false)]
    public void SavedPathsRespectModeAndRoundTrip(bool portable)
    {
        WithStore((_, directory) =>
        {
            Directory.CreateDirectory(directory);
            if (portable) File.WriteAllText(Path.Combine(directory, "portable.flag"), "");
            var store = new Infrastructure.JsonConfigurationStore(portable
                ? Infrastructure.JsonConfigurationStore.DefaultDirectory(directory) : directory);
            var executable = Path.Combine(directory, "streamripper", "streamripper.exe");
            var recordings = Path.Combine(directory, "Nahrávky rádia");
            var configuration = Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration();
            configuration["application"]!["streamripperPath"] = store.PathForStorage(executable, directory);
            configuration["recording"]!["directory"] = store.PathForStorage("Nahrávky rádia", directory);
            store.SaveConfiguration(configuration);
            var loaded = store.LoadConfiguration();
            foreach (var (saved, expected) in new[]
            {
                (loaded["application"]!["streamripperPath"]!.GetValue<string>(), executable),
                (loaded["recording"]!["directory"]!.GetValue<string>(), recordings)
            })
            {
                Xunit.Assert.Equal(!portable, Path.IsPathRooted(saved));
                Xunit.Assert.Equal(expected, Path.GetFullPath(saved, directory));
                if (portable)
                {
                    var moved = Path.Combine(directory, "přesunutá aplikace");
                    Xunit.Assert.Equal(Path.Combine(moved, Path.GetRelativePath(directory, expected)), Path.GetFullPath(saved, moved));
                }
            }
        });
    }

    [Xunit.Fact]
    public void PortablePathsOnDifferentWindowsDrivesAreRejected()
    {
        if (!OperatingSystem.IsWindows()) return;
        WithStore((_, directory) =>
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "portable.flag"), "");
            var store = new Infrastructure.JsonConfigurationStore(Infrastructure.JsonConfigurationStore.DefaultDirectory(directory));
            var otherDrive = Path.GetPathRoot(directory)![0] == 'Z' ? @"Y:\Nahrávky" : @"Z:\Nahrávky";
            Xunit.Assert.Throws<ArgumentException>(() => store.PathForStorage(otherDrive, directory));
        });
    }

    private static void WithStore(Action<Infrastructure.JsonConfigurationStore, string> action)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "configuration-tests", Guid.NewGuid().ToString("N"));
        try { action(new Infrastructure.JsonConfigurationStore(directory), directory); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Xunit.Fact]
    public void LegacyStationFieldsAreRemovedButImportAliasAndUnknownFieldsSurvive()
    {
        WithStore((store, directory) =>
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "stations.json"), """
                {"schemaVersion":1,"stations":[{"id":"radio","name":"Radio","url":"https://example.com/",
                "description":"old","importedCategory":"MP3","importedName":"Original",
                "future":42,"lastObservedStream":{"url":"https://example.com/","audioFormat":"MP3"}}]}
                """);
            var file = store.LoadStations(Infrastructure.JsonConfigurationStore.CreateDefaultConfiguration());
            store.SaveStations(file);
            var saved = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "stations.json")))!["stations"]![0]!;
            Xunit.Assert.Null(saved["description"]);
            Xunit.Assert.Null(saved["importedCategory"]);
            Xunit.Assert.Null(saved["lastObservedStream"]!["url"]);
            Xunit.Assert.Equal("Original", saved["importedName"]!.GetValue<string>());
            Xunit.Assert.Equal(42, saved["future"]!.GetValue<int>());
            Xunit.Assert.Null(new Models.StreamObservation { Url = "https://example.com/" }.ToJson()["url"]);
        });
    }
}
