namespace StreamripperGui.Core.Models;

public sealed record StreamObservation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Url { get; init; } = "";
    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Transport { get; init; } = "unknown";
    public string AudioFormat { get; init; } = "unknown";
    public string Container { get; init; } = "unknown";
    public string TrackTitles { get; init; } = "unknown";
    public string MetadataRules { get; init; } = "unknown";
    public int? DeclaredBitrate { get; init; }
    public int? MetadataInterval { get; init; }
    public int? RelayPort { get; init; }
    public string? RelayAddress { get; init; }
    public string NativeFormat { get; init; } = "";

    public System.Text.Json.Nodes.JsonObject ToJson() =>
        System.Text.Json.JsonSerializer.SerializeToNode(this, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.AsObject();
}

/// <summary>Parses bounded startup lines independently of UI refresh, scrolling and selection.</summary>
public sealed class StreamSummaryParser(string url)
{
    private readonly System.Text.Decoder _decoder = System.Text.Encoding.UTF8.GetDecoder();
    private readonly System.Text.StringBuilder _line = new();
    private StreamObservation _observation = new() { Url = url };
    private bool _discardLine;
    public event Action<StreamObservation>? Observed;

    public void Append(ReadOnlySpan<byte> bytes)
    {
        Span<char> characters = stackalloc char[512];
        while (!bytes.IsEmpty)
        {
            _decoder.Convert(bytes, characters, false, out var consumed, out var produced, out _);
            bytes = bytes[consumed..];
            foreach (var character in characters[..produced])
            {
                if (character is '\n' or '\r')
                {
                    if (!_discardLine && _line.Length != 0) Parse(_line.ToString().Trim());
                    _line.Clear();
                    _discardLine = false;
                }
                else if (_line.Length < 4096) _line.Append(character);
                else _discardLine = true;
            }
        }
    }

    private void Parse(string line)
    {
        var separator = line.IndexOf(':');
        if (separator < 0) return;
        var key = line[..separator];
        var value = line[(separator + 1)..].Trim();
        switch (key)
        {
            case "format":
                var hls = value.Contains("HLS", StringComparison.OrdinalIgnoreCase);
                var audio = value.StartsWith("MP3", StringComparison.OrdinalIgnoreCase) ? "MP3"
                    : value.StartsWith("AAC", StringComparison.OrdinalIgnoreCase) ? "AAC"
                    : value.StartsWith("FLAC", StringComparison.OrdinalIgnoreCase) ? "FLAC"
                    : value.StartsWith("Vorbis", StringComparison.OrdinalIgnoreCase) ? "Vorbis" : "unknown";
                var ogg = value.Contains("Ogg", StringComparison.OrdinalIgnoreCase);
                _observation = _observation with
                {
                    NativeFormat = value, AudioFormat = audio, Transport = hls ? "HLS" : "unknown",
                    Container = ogg ? "Ogg" : value.Contains("MPEG-TS", StringComparison.OrdinalIgnoreCase) ? "MPEG-TS" : "unknown",
                    MetadataRules = hls || ogg || audio is "FLAC" or "Vorbis" ? "unsupported"
                        : audio is "MP3" or "AAC" ? "supported" : "unknown"
                };
                break;
            case "declared bitrate" when int.TryParse(value, out var bitrate):
                _observation = _observation with { DeclaredBitrate = bitrate };
                break;
            case "meta interval" when int.TryParse(value, out var interval):
                _observation = _observation with { MetadataInterval = interval };
                break;
            case "relay port" when int.TryParse(value, out var port):
                _observation = _observation with { RelayPort = port };
                break;
            case "relay" when System.Net.IPEndPoint.TryParse(value, out var endpoint):
                _observation = _observation with { RelayPort = endpoint.Port, RelayAddress = endpoint.Address.ToString() };
                break;
            default: return;
        }
        if (_observation.NativeFormat.Length == 0) return;
        var titles = _observation.Transport == "HLS" || _observation.Container == "Ogg" ? "unknown"
            : _observation.MetadataInterval is > 0 ? "available"
            : _observation.MetadataInterval == 0 && _observation.AudioFormat is "MP3" or "AAC" ? "unavailable" : "unknown";
        _observation = _observation with { TrackTitles = titles };
        Observed?.Invoke(_observation);
    }
}
