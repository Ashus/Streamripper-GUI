namespace StreamripperGui.Core.Models;

public sealed record RecordingOptions
{
    public string FilenamePattern { get; init; } = "%q - %A - %T";
    public string RecordingMode { get; init; } = "individual";
    public bool Wav { get; init; }
    public int SkipInitialTracks { get; init; } = 1;
    public bool KeepIncomplete { get; init; } = true;
    public int IncompleteMinDurationSeconds { get; init; } = 900;
    public string Overwrite { get; init; } = "version";
    public int TimeoutSeconds { get; init; } = 8;
    public bool Reconnect { get; init; } = true;
    public string UserAgentMode { get; init; } = "winamp";
    public string CustomUserAgent { get; init; } = "";
    public string IpFamily { get; init; } = "auto";
    public bool RelayEnabled { get; init; }
    public int RelayPort { get; init; } = 8000;
    public int MaxRelayClients { get; init; } = 10;
    public string RelayBindAddress { get; init; } = "";
    public int RecordDurationSeconds { get; init; }
    public int MaxMegabytes { get; init; }
    public bool Id3V1 { get; init; }
    public bool Id3V2 { get; init; } = true;
    public string MetadataEncoding { get; init; } = "AUTO";
    public string SilenceDetector { get; init; } = "standard";
    public string SplitOffsetMilliseconds { get; init; } = "";
    public string SplitPadding { get; init; } = "";
    public string SplitSearchWindow { get; init; } = "";
    public string SplitSilenceMilliseconds { get; init; } = "";
    public string Proxy { get; init; } = "";
    public bool Http10 { get; init; }
    public string TlsCaFile { get; init; } = "";
    public bool InsecureTls { get; init; }
    public string HlsMode { get; init; } = "best";
    public int HlsBitrate { get; init; } = 128000;

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; init; }
}

public static class RecordingOptionsJson
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);
    public static RecordingOptions Read(System.Text.Json.Nodes.JsonNode? node) =>
        System.Text.Json.JsonSerializer.Deserialize<RecordingOptions>(node, Options) ?? throw new InvalidDataException("Recording options are missing.");
    public static System.Text.Json.Nodes.JsonObject Write(RecordingOptions options) =>
        System.Text.Json.JsonSerializer.SerializeToNode(options, Options)!.AsObject();
}
