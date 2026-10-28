namespace StreamripperGui.Core.Models;

public static class RecordingArguments
{
    public static void Validate(RecordingOptions options)
    {
        static void Choice(string value, params string[] allowed)
        {
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new ArgumentException($"Unsupported option: {value}.");
        }
        static void Range(int value, int min, int max, string name)
        {
            if (value < min || value > max) throw new ArgumentException($"{name} must be between {min} and {max}.");
        }
        Choice(options.RecordingMode, "individual", "continuous", "both");
        Choice(options.Overwrite, "always", "never", "larger", "version");
        Choice(options.UserAgentMode, "streamripper", "winamp", "custom");
        Choice(options.IpFamily, "auto", "ipv4", "ipv6");
        Choice(options.SilenceDetector, "standard", "alternative", "off");
        Choice(options.HlsMode, "best", "worst", "bitrate");
        if (string.IsNullOrWhiteSpace(options.FilenamePattern) ||
            options.FilenamePattern.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)) ||
            options.FilenamePattern.TrimEnd('.', ' ') != options.FilenamePattern)
            throw new ArgumentException("The filename pattern must be a filename, without directories, invalid characters or trailing dots/spaces.");
        const string tokens = "ASTaDdq%";
        for (var i = 0; i < options.FilenamePattern.Length; i++)
        {
            if (options.FilenamePattern[i] != '%') continue;
            i++;
            if (i < options.FilenamePattern.Length && char.IsAsciiDigit(options.FilenamePattern[i]))
            {
                while (i < options.FilenamePattern.Length && char.IsAsciiDigit(options.FilenamePattern[i])) i++;
                if (i >= options.FilenamePattern.Length || options.FilenamePattern[i] != 'q')
                    throw new ArgumentException("A sequence starting number must end with q.");
            }
            else if (i >= options.FilenamePattern.Length || !tokens.Contains(options.FilenamePattern[i]))
                throw new ArgumentException("The filename pattern contains an unsupported % token.");
        }
        Range(options.TimeoutSeconds, 1, 86400, "Timeout");
        Range(options.SkipInitialTracks, 0, 1000000, "Initial incomplete tracks");
        Range(options.IncompleteMinDurationSeconds, 0, int.MaxValue, "Minimum incomplete duration");
        Range(options.RecordDurationSeconds, 0, int.MaxValue, "Recording duration");
        Range(options.MaxMegabytes, 0, int.MaxValue, "Maximum megabytes");
        if (options.UserAgentMode == "custom" && string.IsNullOrWhiteSpace(options.CustomUserAgent))
            throw new ArgumentException("Enter a custom User-Agent.");
        foreach (var text in new[] { options.CustomUserAgent, options.MetadataEncoding, options.Proxy, options.TlsCaFile, options.RelayBindAddress })
            if (text.Any(c => c < 32)) throw new ArgumentException("Option values cannot contain control characters.");
        if (string.IsNullOrWhiteSpace(options.MetadataEncoding)) throw new ArgumentException("Enter a metadata encoding or AUTO.");
        if (options.RelayEnabled)
        {
            Range(options.RelayPort, 1, 65535, "Relay port");
            Range(options.MaxRelayClients, 0, int.MaxValue, "Relay clients");
            if (options.RelayBindAddress.Length > 0 && !System.Net.IPAddress.TryParse(options.RelayBindAddress, out _))
                throw new ArgumentException("Relay bind address must be an IPv4 or IPv6 address.");
        }
        if (options.Proxy.Length > 0 &&
            (!Uri.TryCreate(options.Proxy, UriKind.Absolute, out var proxy) || proxy.Scheme is not ("http" or "https")))
            throw new ArgumentException("Proxy must be an HTTP or HTTPS URL.");
        if (options.HlsMode == "bitrate") Range(options.HlsBitrate, 1, int.MaxValue, "HLS bitrate");
        ParseOptionalNumber(options.SplitOffsetMilliseconds, signed: true);
        ParseOptionalNumber(options.SplitSilenceMilliseconds);
        ParsePair(options.SplitPadding);
        ParsePair(options.SplitSearchWindow);
    }

    private static void ParseOptionalNumber(string text, bool signed = false)
    {
        if (text.Length == 0) return;
        if (!int.TryParse(text, System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out var number) || (!signed && number < 0))
            throw new ArgumentException($"Invalid millisecond value: {text}.");
    }

    private static void ParsePair(string text)
    {
        if (text.Length == 0) return;
        var values = text.Split(':');
        if (values.Length != 2 || values.Any(string.IsNullOrEmpty)) throw new ArgumentException("Use two nonnegative millisecond values separated by a colon.");
        foreach (var value in values) ParseOptionalNumber(value);
    }

    public static string[] Build(string url, string destination, RecordingOptions options, string? metadataRulesPath = null)
    {
        Validate(options);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Enter an HTTP(S) stream URL.");
        var result = new List<string>
        {
            uri.AbsoluteUri, "--output-directory", destination, "--no-stream-directory",
            "--metadata-log", "--tls-info"
        };
        if (metadataRulesPath is not null)
        {
            result.Add("--metadata-rules");
            result.Add(metadataRulesPath);
            result.Add("--reload-rules");
        }
        result.AddRange(["--output-pattern", options.FilenamePattern, $"--timeout={options.TimeoutSeconds}",
            $"--skip-initial-tracks={options.SkipInitialTracks}", $"--incomplete-min-duration={options.IncompleteMinDurationSeconds}", $"--overwrite={options.Overwrite}"]);
        if (options.RecordingMode is "continuous" or "both") result.Add("--continuous-file");
        if (options.RecordingMode == "continuous") result.Add("--no-individual-tracks");
        if (options.Wav) result.Add("--wav");
        if (options.KeepIncomplete) result.Add("--keep-incomplete");
        if (!options.Reconnect) result.Add("--no-reconnect");
        if (options.UserAgentMode != "streamripper")
        {
            result.Add("--user-agent");
            result.Add(options.UserAgentMode == "winamp" ? "WinampMPEG/5.0" : options.CustomUserAgent);
        }
        if (options.IpFamily != "auto") result.Add("--" + options.IpFamily);
        if (options.RelayEnabled)
        {
            var address = options.RelayBindAddress;
            if (address.Contains(':')) address = "[" + address + "]";
            result.Add("--relay=" + (address.Length == 0 ? "" : address + ":") + options.RelayPort);
            result.Add($"--max-relay-clients={options.MaxRelayClients}");
        }
        if (options.RecordDurationSeconds > 0) result.Add($"--record-duration={options.RecordDurationSeconds}");
        if (options.MaxMegabytes > 0) result.Add($"--max-megabytes={options.MaxMegabytes}");
        if (options.Wav || (!options.Id3V1 && !options.Id3V2)) result.Add("--no-id3");
        else
        {
            if (options.Id3V1) result.Add("--with-id3v1");
            if (!options.Id3V2) result.Add("--without-id3v2");
        }
        result.Add("--codeset-metadata=" + options.MetadataEncoding);
        if (options.SilenceDetector == "off") result.Add("--xs-none");
        if (options.SilenceDetector == "alternative") result.Add("--xs2");
        foreach (var (name, value) in new[]
        {
            ("--xs-offset=", options.SplitOffsetMilliseconds), ("--xs-padding=", options.SplitPadding),
            ("--xs-search-window=", options.SplitSearchWindow), ("--xs-silence-length=", options.SplitSilenceMilliseconds)
        })
            if (value.Length != 0) result.Add(name + value);
        if (options.Proxy.Length != 0) { result.Add("--proxy"); result.Add(options.Proxy); }
        if (options.Http10) result.Add("--http10");
        if (options.TlsCaFile.Length != 0) result.Add("--tls-ca-file=" + options.TlsCaFile);
        if (options.InsecureTls) result.Add("--insecure");
        result.Add("--hls-variant=" + (options.HlsMode == "bitrate" ? options.HlsBitrate.ToString(System.Globalization.CultureInfo.InvariantCulture) : options.HlsMode));
        return result.ToArray();
    }
}
