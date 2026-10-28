namespace StreamripperGui.Core.Tests;

public sealed class RecordingProfileTests
{
    [Xunit.Fact]
    public void DefaultsKeepRelayClosedAndForceDiagnostics()
    {
        var args = Build(new());
        Xunit.Assert.Equal("https://example.com/stream", args[0]);
        Xunit.Assert.Contains("--metadata-log", args);
        Xunit.Assert.Contains("--tls-info", args);
        Xunit.Assert.Contains("--timeout=8", args);
        Xunit.Assert.Contains("--incomplete-min-duration=900", args);
        Xunit.Assert.Contains("WinampMPEG/5.0", args);
        Xunit.Assert.DoesNotContain(args, a => a.StartsWith("--relay", StringComparison.Ordinal));
        Xunit.Assert.DoesNotContain("--max-relay-clients=10", args);
        Xunit.Assert.DoesNotContain("--metadata-rules", args);
        Xunit.Assert.DoesNotContain("--config", args);
        Xunit.Assert.DoesNotContain("--quiet", args);
    }

    [Xunit.Theory]
    [Xunit.InlineData("individual", false, false)]
    [Xunit.InlineData("continuous", true, true)]
    [Xunit.InlineData("both", true, false)]
    public void RecordingModesGenerateOneConsistentCombination(string mode, bool continuous, bool noTracks)
    {
        var args = Build(new() { RecordingMode = mode });
        Xunit.Assert.Equal(continuous, args.Contains("--continuous-file"));
        Xunit.Assert.Equal(noTracks, args.Contains("--no-individual-tracks"));
    }

    [Xunit.Theory]
    [Xunit.InlineData("auto", false, false)]
    [Xunit.InlineData("ipv4", true, false)]
    [Xunit.InlineData("ipv6", false, true)]
    public void IpFamiliesAreExclusive(string family, bool ipv4, bool ipv6)
    {
        var args = Build(new() { IpFamily = family });
        Xunit.Assert.Equal(ipv4, args.Contains("--ipv4"));
        Xunit.Assert.Equal(ipv6, args.Contains("--ipv6"));
    }

    [Xunit.Fact]
    public void UserAgentIsEitherOmittedOrPassedLiterally()
    {
        Xunit.Assert.DoesNotContain("--user-agent", Build(new() { UserAgentMode = "streamripper" }));
        const string custom = "Player \"quoted\" & literal";
        var args = Build(new() { UserAgentMode = "custom", CustomUserAgent = custom });
        Xunit.Assert.Equal(custom, args[Array.IndexOf(args, "--user-agent") + 1]);
        Xunit.Assert.Throws<ArgumentException>(() => Build(new() { UserAgentMode = "custom" }));
    }

    [Xunit.Fact]
    public void RelayUsesIpv6SyntaxWithoutDisablingNativePortSearch()
    {
        var args = Build(new() { RelayEnabled = true, RelayBindAddress = "::1", RelayPort = 8000, MaxRelayClients = 10 });
        Xunit.Assert.Contains("--relay=[::1]:8000", args);
        Xunit.Assert.Contains("--max-relay-clients=10", args);
        Xunit.Assert.DoesNotContain("--no-port-scan", args);
    }

    [Xunit.Fact]
    public void WavDisablesTagsForLaunchWithoutErasingProfilePreferences()
    {
        var options = new Models.RecordingOptions { Wav = true, Id3V1 = true, Id3V2 = true };
        var args = Build(options);
        Xunit.Assert.Contains("--no-id3", args);
        Xunit.Assert.DoesNotContain("--with-id3v1", args);
        Xunit.Assert.True(options.Id3V1);
        Xunit.Assert.True(options.Id3V2);
    }

    [Xunit.Fact]
    public void AdvancedOptionsAndBothLimitsMapToExactArguments()
    {
        var args = Build(new()
        {
            RecordDurationSeconds = 100, MaxMegabytes = 200, SplitOffsetMilliseconds = "-50",
            SplitPadding = "10:20", SplitSearchWindow = "4000:6000", SplitSilenceMilliseconds = "1000",
            SilenceDetector = "alternative", HlsMode = "bitrate", HlsBitrate = 256000,
            Reconnect = false, Id3V1 = true, Id3V2 = false, Http10 = true,
            Proxy = "http://localhost:8080", TlsCaFile = "CA files/trusted.pem", InsecureTls = true
        });
        foreach (var expected in new[] { "--record-duration=100", "--max-megabytes=200", "--xs-offset=-50",
                     "--xs-padding=10:20", "--xs-search-window=4000:6000", "--xs-silence-length=1000",
                     "--xs2", "--hls-variant=256000", "--no-reconnect", "--with-id3v1",
                     "--without-id3v2", "--http10", "http://localhost:8080", "--tls-ca-file=CA files/trusted.pem", "--insecure" })
            Xunit.Assert.Contains(expected, args);
    }

    [Xunit.Theory]
    [Xunit.InlineData("../outside")]
    [Xunit.InlineData("subdir/track")]
    [Xunit.InlineData("%X")]
    [Xunit.InlineData("%12A")]
    [Xunit.InlineData("%")]
    [Xunit.InlineData("name. ")]
    public void InvalidFilenamePatternsAreRejected(string pattern) =>
        Xunit.Assert.Throws<ArgumentException>(() => Build(new() { FilenamePattern = pattern }));

    [Xunit.Fact]
    public void NumberedAndEscapedFilenameTokensRemainAvailable()
    {
        Xunit.Assert.Contains("%% %A - %T - %12q", Build(new() { FilenamePattern = "%% %A - %T - %12q" }));
    }

    [Xunit.Fact]
    public void InvalidSelectionsAndNumbersAreRejectedBeforeLaunch()
    {
        foreach (var options in new Models.RecordingOptions[]
                 {
                     new() { RecordingMode = "none" }, new() { TimeoutSeconds = 0 },
                     new() { RelayEnabled = true, RelayPort = 65536 }, new() { RelayEnabled = true, RelayBindAddress = "hostname" },
                     new() { SplitPadding = "-1:3" }, new() { SplitSearchWindow = "12" },
                     new() { CustomUserAgent = "one\r\ntwo" }, new() { Proxy = "ftp://example.com" }
                 })
            Xunit.Assert.Throws<ArgumentException>(() => Build(options));
    }

    [Xunit.Fact]
    public void JsonKeepsUnknownOptionsAndSnapshotsAreIndependent()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse("""{"timeoutSeconds":8,"futureOption":true}""");
        var original = Models.RecordingOptionsJson.Read(node);
        var changed = original with { TimeoutSeconds = 20 };
        Xunit.Assert.Equal(8, original.TimeoutSeconds);
        Xunit.Assert.True(Models.RecordingOptionsJson.Write(changed)["futureOption"]!.GetValue<bool>());
    }

    [Xunit.Fact]
    public void StartupSummarySurvivesEveryByteBoundaryAndDoesNotGuessFromUrl()
    {
        var parser = new Models.StreamSummaryParser("https://example.com/looks-like.flac");
        Models.StreamObservation? last = null;
        parser.Observed += value => last = value;
        var bytes = System.Text.Encoding.UTF8.GetBytes("stream: Žluťoučké rádio\r\nformat: MP3\r\ndeclared bitrate: 320\nmeta interval: 16000\nrelay port: 8001\n[ripping...] title\r[filtered...] title\r");
        foreach (var value in bytes) parser.Append([value]);
        Xunit.Assert.NotNull(last);
        Xunit.Assert.Equal("MP3", last.AudioFormat);
        Xunit.Assert.Equal("supported", last.MetadataRules);
        Xunit.Assert.Equal("available", last.TrackTitles);
        Xunit.Assert.Equal(8001, last.RelayPort);
        Xunit.Assert.Equal(320, last.DeclaredBitrate);
        Xunit.Assert.Equal("unknown", last.Transport);
    }

    [Xunit.Theory]
    [Xunit.InlineData("AAC (ADTS, HLS)", "AAC", "HLS", "unsupported")]
    [Xunit.InlineData("FLAC (Ogg)", "FLAC", "unknown", "unsupported")]
    [Xunit.InlineData("Vorbis (Ogg)", "Vorbis", "unknown", "unsupported")]
    [Xunit.InlineData("HLS (MPEG-TS)", "unknown", "HLS", "unsupported")]
    [Xunit.InlineData("unknown", "unknown", "unknown", "unknown")]
    public void SummaryDistinguishesTransportAudioAndRuleCapabilities(string format, string audio, string transport, string rules)
    {
        var parser = new Models.StreamSummaryParser("https://example.com/stream");
        Models.StreamObservation? last = null;
        parser.Observed += value => last = value;
        parser.Append(System.Text.Encoding.UTF8.GetBytes($"format: {format}\n"));
        Xunit.Assert.NotNull(last);
        Xunit.Assert.Equal(audio, last.AudioFormat);
        Xunit.Assert.Equal(transport, last.Transport);
        Xunit.Assert.Equal(rules, last.MetadataRules);
        Xunit.Assert.Equal("unknown", last.TrackTitles);
    }

    [Xunit.Theory]
    [Xunit.InlineData("127.0.0.1:8002")]
    [Xunit.InlineData("[::1]:8002")]
    public void ExplicitRelayEndpointsAreReported(string endpoint)
    {
        var parser = new Models.StreamSummaryParser("https://example.com");
        Models.StreamObservation? last = null;
        parser.Observed += value => last = value;
        parser.Append(System.Text.Encoding.UTF8.GetBytes($"format: MP3\nrelay: {endpoint}\n"));
        Xunit.Assert.Equal(8002, last?.RelayPort);
    }

    private static string[] Build(Models.RecordingOptions options) =>
        Models.RecordingArguments.Build("https://example.com/stream", "Radio files/Žluťoučký", options);
}
