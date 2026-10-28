namespace StreamripperGui.Core.Tests;

public sealed class OpmlImportTests
{
    [Xunit.Fact]
    public void RealMusicBeeExportKeepsEveryStationAndTreatsFormatsAsHints()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "musicbee-stations.opml"));
        var entries = Infrastructure.OpmlReader.Read(stream);
        Xunit.Assert.Equal(8, entries.Count);
        Xunit.Assert.Contains(entries, s => s.Url.EndsWith(".pls", StringComparison.Ordinal));
        Xunit.Assert.Contains(entries, s => s.Name == "1.FM High Voltage Radio" && s.Url.Contains("?aw_0_req.gdpr=true", StringComparison.Ordinal));
        var plan = Models.OpmlImportPlanner.Prepare([], entries, false);
        Xunit.Assert.Equal(8, plan.Stations.Count);
        Xunit.Assert.All(plan.Stations, station =>
        {
            Xunit.Assert.Null(station.LastObservedStream);
            Xunit.Assert.Null(station.RecordingProfileId);
            Xunit.Assert.Null(station.MetadataRuleSetId);
        });
        Xunit.Assert.Contains(plan.Stations, s => s.Name == "Paradise (FLAC)");
    }

    [Xunit.Fact]
    public void NestedGroupsUnicodeEscapingAndPlaylistQueryArePreserved()
    {
        var entries = Read("""
        <opml version="2.0"><body><outline text="Group"><outline text="Žluťoučké rádio &amp; Jazz" type="link"
        url="https://example.com/listen.pls?x=1&amp;y=2" category="Jazz/MP3" description="Popis"/></outline></body></opml>
        """);
        var station = Xunit.Assert.Single(entries);
        Xunit.Assert.Equal("Žluťoučké rádio & Jazz", station.Name);
        Xunit.Assert.Equal("https://example.com/listen.pls?x=1&y=2", station.Url);
        Xunit.Assert.Equal("Popis", station.Description);
        Xunit.Assert.Equal("Jazz/MP3", station.Category);
    }

    [Xunit.Theory]
    [Xunit.InlineData("<rss><channel/></rss>")]
    [Xunit.InlineData("<opml version='2.0'><body>")]
    [Xunit.InlineData("<opml version='2.0'/>")]
    [Xunit.InlineData("<opml version='2.0'><body/></opml>")]
    [Xunit.InlineData("<opml version='2.0'><body><outline type='link' text='No URL'/></body></opml>")]
    [Xunit.InlineData("<opml version='2.0'><body><outline type='link' url='https://example.com'/></body></opml>")]
    [Xunit.InlineData("<opml version='2.0'><body><outline type='link' text='Radio' url='file:///c:/test'/></body></opml>")]
    [Xunit.InlineData("<opml version='2.0'><body><outline type='rss' text='Radio' url='https://example.com'/></body></opml>")]
    [Xunit.InlineData("<!DOCTYPE opml [<!ENTITY x SYSTEM 'file:///c:/test'>]><opml version='2.0'><body/></opml>")]
    public void InvalidIncompleteAndExternalEntityDocumentsAreRejected(string xml) =>
        Xunit.Assert.ThrowsAny<Exception>(() => Read(xml));

    [Xunit.Fact]
    public void MergeUsesLastCaseInsensitiveNameAndPreservesIdsAssignmentsAndUnchangedObservation()
    {
        var observation = new System.Text.Json.Nodes.JsonObject { ["audioFormat"] = "MP3" };
        var existing = new Models.Station
        {
            Name = "Jazz", Url = "https://example.com/original",
            RecordingProfileId = "profile", MetadataRuleSetId = "rules", LastObservedStream = observation
        };
        var other = new Models.Station { Name = "Other", Url = "https://example.com/other" };
        var plan = Models.OpmlImportPlanner.Prepare([existing, other],
            [new(" jazz ", "https://example.com/temporary"), new("JAZZ", existing.Url)], false);
        var updated = plan.Stations.Single(s => s.Id == existing.Id);
        Xunit.Assert.Equal("JAZZ", updated.Name);
        Xunit.Assert.Equal(existing.Url, updated.Url);
        Xunit.Assert.Equal("profile", updated.RecordingProfileId);
        Xunit.Assert.Equal("rules", updated.MetadataRuleSetId);
        Xunit.Assert.Same(observation, updated.LastObservedStream);
        Xunit.Assert.Contains(other, plan.Stations);
        Xunit.Assert.Single(plan.Changes);
    }

    [Xunit.Fact]
    public void ChangedUrlDropsWholeObservationWithoutMutatingOriginal()
    {
        var observation = new System.Text.Json.Nodes.JsonObject { ["audioFormat"] = "MP3", ["relayPort"] = 8000 };
        var existing = new Models.Station { Name = "Jazz", Url = "https://example.com/old", LastObservedStream = observation };
        var updated = Xunit.Assert.Single(Models.OpmlImportPlanner.Prepare([existing], [new("Jazz", "https://example.com/new")], false).Stations);
        Xunit.Assert.Null(updated.LastObservedStream);
        Xunit.Assert.Same(observation, existing.LastObservedStream);
        Xunit.Assert.Equal("https://example.com/old", existing.Url);
    }

    [Xunit.Fact]
    public void ReplacementReconcilesBeforeRemovingUnmatchedStations()
    {
        var keep = new Models.Station { Name = "Keep", Url = "https://example.com/a", RecordingProfileId = "profile", MetadataRuleSetId = "filter" };
        var remove = new Models.Station { Name = "Remove", Url = "https://example.com/remove" };
        var plan = Models.OpmlImportPlanner.Prepare([keep, remove], [new("keep", "https://example.com/updated"), new("New", "https://example.com/new")], true);
        Xunit.Assert.Equal(2, plan.Stations.Count);
        Xunit.Assert.DoesNotContain(plan.Stations, s => s.Id == remove.Id);
        var matched = plan.Stations.Single(s => s.Id == keep.Id);
        Xunit.Assert.Equal("profile", matched.RecordingProfileId);
        Xunit.Assert.Equal("filter", matched.MetadataRuleSetId);
        Xunit.Assert.Contains(plan.Changes, c => c.Action == "Remove" && c.Name == "Remove");
    }

    [Xunit.Fact]
    public void CollisionSuffixesAreDeterministicAndRepeatImportsKeepIds()
    {
        Models.OpmlStation[] entries = [new("Radio: Jazz", "https://example.com/a"), new("Radio? Jazz", "https://example.com/b")];
        var first = Models.OpmlImportPlanner.Prepare([], entries, false);
        Xunit.Assert.Equal(["Radio: Jazz", "Radio? Jazz (2)"], first.Stations.Select(s => s.Name).ToArray());
        var again = Models.OpmlImportPlanner.Prepare(first.Stations, entries, false);
        Xunit.Assert.Equal(first.Stations.Select(s => s.Id), again.Stations.Select(s => s.Id));
        Xunit.Assert.Equal(2, again.Stations.Count);
        var renamed = Models.StationCatalog.Edit(again.Stations[1], "Manual name", again.Stations[1].Url, again.Stations);
        Xunit.Assert.Null(renamed.ImportedName);
    }

    [Xunit.Fact]
    public void MatchingUsesDisplayNamesBeforeSanitizedFolders()
    {
        var first = new Models.Station { Name = "Radio: Jazz", Url = "https://example.com/a" };
        var second = new Models.Station { Name = "Other", Url = "https://example.com/other" };
        var plan = Models.OpmlImportPlanner.Prepare([first, second], [new("Other", "https://example.com/changed"), new("Radio? Jazz", "https://example.com/b")], false);
        Xunit.Assert.Equal(3, plan.Stations.Count);
        Xunit.Assert.Equal(first.Url, plan.Stations.Single(s => s.Id == first.Id).Url);
        Xunit.Assert.Equal("https://example.com/changed", plan.Stations.Single(s => s.Id == second.Id).Url);
        Xunit.Assert.Contains(plan.Stations, s => s.Name == "Radio? Jazz (2)");
    }

    [Xunit.Fact]
    public void EmptySelectionAndInvalidEntriesCannotProduceReplacement()
    {
        var existing = new Models.Station { Name = "Existing", Url = "https://example.com" };
        Xunit.Assert.Throws<ArgumentException>(() => Models.OpmlImportPlanner.Prepare([existing], [], true));
        Xunit.Assert.Throws<ArgumentException>(() => Models.OpmlImportPlanner.Prepare([existing], [new("Radio", "bad URL")], true));
        Xunit.Assert.Equal("Existing", existing.Name);
    }

    private static IReadOnlyList<Models.OpmlStation> Read(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return Infrastructure.OpmlReader.Read(stream);
    }

    [Xunit.Fact]
    public void ImportAliasUpdatesUrlWithoutMatchingByOldUrl()
    {
        var existing = new Models.Station { Name = "Radio (2)", ImportedName = "Radio", Url = "https://example.com/old", RecordingProfileId = "profile" };
        var updated = Xunit.Assert.Single(Models.OpmlImportPlanner.Prepare([existing], [new("Radio", "https://example.com/new")], false).Stations);
        Xunit.Assert.Equal(existing.Id, updated.Id);
        Xunit.Assert.Equal("https://example.com/new", updated.Url);
        Xunit.Assert.Equal("profile", updated.RecordingProfileId);
        Xunit.Assert.Equal("Radio (2)", updated.Name);
    }
}
