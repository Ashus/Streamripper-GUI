namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class RecordingOptionsViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private Core.Models.RecordingOptions _original = new();
    public Core.Models.RecordingOptions Baseline { get; private set; } = new();

    public void SetBaseline(Core.Models.RecordingOptions baseline)
    {
        Baseline = baseline;
        OnPropertyChanged(nameof(Baseline));
    }

    public bool IsNonDefault(string property)
    {
        var source = typeof(RecordingOptionsViewModel).GetProperty(property);
        var baseline = typeof(Core.Models.RecordingOptions).GetProperty(property);
        return source is not null && baseline is not null && !Equals(source.GetValue(this), baseline.GetValue(Baseline));
    }

    public RecordingOptionsViewModel() => Load(new());

    public IReadOnlyList<OptionChoice> RecordingModeChoices { get; } = [new("individual", Localization.Localizer.Current.Message("IndividualTracks")), new("continuous", Localization.Localizer.Current.Message("ContinuousRecording")), new("both", Localization.Localizer.Current.Message("TracksAndContinuousRecording"))];
    public OptionChoice? SelectedRecordingMode
    {
        get => RecordingModeChoices.FirstOrDefault(choice => choice.Value == RecordingMode);
        set { if (value is not null) RecordingMode = value.Value; }
    }
    public IReadOnlyList<OptionChoice> OverwriteChoices { get; } = [new("always", Localization.Localizer.Current.Message("Always")), new("never", Localization.Localizer.Current.Message("Never")), new("larger", Localization.Localizer.Current.Message("LargerFile")), new("version", Localization.Localizer.Current.Message("CreateAVersion"))];
    public OptionChoice? SelectedOverwrite
    {
        get => OverwriteChoices.FirstOrDefault(choice => choice.Value == Overwrite);
        set { if (value is not null) Overwrite = value.Value; }
    }
    public IReadOnlyList<OptionChoice> UserAgentModeChoices { get; } = [new("streamripper", Localization.Localizer.Current.Message("StreamripperDefault")), new("winamp", Localization.Localizer.Current.Message("WinampCompatibility")), new("custom", Localization.Localizer.Current.Message("Custom"))];
    public OptionChoice? SelectedUserAgentMode
    {
        get => UserAgentModeChoices.FirstOrDefault(choice => choice.Value == UserAgentMode);
        set { if (value is not null) UserAgentMode = value.Value; }
    }
    public IReadOnlyList<OptionChoice> IpFamilyChoices { get; } =
        [new("auto", Localization.Localizer.Current.Message("Automatic")), new("ipv4", Localization.Localizer.Current.Message("IpV4")), new("ipv6", Localization.Localizer.Current.Message("IpV6"))];
    public OptionChoice? SelectedIpFamily
    {
        get => IpFamilyChoices.FirstOrDefault(choice => choice.Value == IpFamily);
        set { if (value is not null) IpFamily = value.Value; }
    }
    public IReadOnlyList<OptionChoice> SilenceDetectorChoices { get; } = [new("standard", Localization.Localizer.Current.Message("Standard")), new("alternative", Localization.Localizer.Current.Message("AlternativeMP3")), new("off", Localization.Localizer.Current.Message("Off"))];
    public OptionChoice? SelectedSilenceDetector
    {
        get => SilenceDetectorChoices.FirstOrDefault(choice => choice.Value == SilenceDetector);
        set { if (value is not null) SilenceDetector = value.Value; }
    }
    public IReadOnlyList<OptionChoice> HlsModeChoices { get; } = [new("best", Localization.Localizer.Current.Message("Best")), new("worst", Localization.Localizer.Current.Message("Worst")), new("bitrate", Localization.Localizer.Current.Message("TargetBitrate"))];
    public OptionChoice? SelectedHlsMode
    {
        get => HlsModeChoices.FirstOrDefault(choice => choice.Value == HlsMode);
        set { if (value is not null) HlsMode = value.Value; }
    }

    public bool IsCustomUserAgent => UserAgentMode == "custom";
    public bool IsHlsBitrate => HlsMode == "bitrate";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string FilenamePattern { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedRecordingMode))]
    public partial string RecordingMode { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Wav { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int SkipInitialTracks { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool KeepIncomplete { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IncompleteDurationValue))]
    public partial int IncompleteMinDurationSeconds { get; set; }

    public IReadOnlyList<string> DurationUnits { get; } = ["s", "m", "h"];
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IncompleteDurationValue))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IncompleteDurationMaximum))]
    public partial string IncompleteDurationUnit { get; set; } = "m";
    private int IncompleteDurationFactor => IncompleteDurationUnit switch { "h" => 3600, "m" => 60, _ => 1 };
    public decimal IncompleteDurationMaximum => (decimal)int.MaxValue / IncompleteDurationFactor;
    public decimal IncompleteDurationValue
    {
        get => (decimal)IncompleteMinDurationSeconds / IncompleteDurationFactor;
        set => IncompleteMinDurationSeconds = checked((int)decimal.Round(value * IncompleteDurationFactor, 0, MidpointRounding.AwayFromZero));
    }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedOverwrite))]
    public partial string Overwrite { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int TimeoutSeconds { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Reconnect { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IsCustomUserAgent))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedUserAgentMode))]
    public partial string UserAgentMode { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string CustomUserAgent { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedIpFamily))]
    public partial string IpFamily { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool RelayEnabled { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int RelayPort { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int MaxRelayClients { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string RelayBindAddress { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int RecordDurationSeconds { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int MaxMegabytes { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Id3V1 { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Id3V2 { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string MetadataEncoding { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedSilenceDetector))]
    public partial string SilenceDetector { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SplitOffsetMilliseconds { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SplitPadding { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SplitSearchWindow { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string SplitSilenceMilliseconds { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Proxy { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Http10 { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string TlsCaFile { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool InsecureTls { get; set; }

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(IsHlsBitrate))]
    [CommunityToolkit.Mvvm.ComponentModel.NotifyPropertyChangedFor(nameof(SelectedHlsMode))]
    public partial string HlsMode { get; set; } = "";

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial int HlsBitrate { get; set; }

    public void Load(Core.Models.RecordingOptions options)
    {
        _original = options;
        FilenamePattern = options.FilenamePattern;
        RecordingMode = options.RecordingMode;
        Wav = options.Wav;
        SkipInitialTracks = options.SkipInitialTracks;
        KeepIncomplete = options.KeepIncomplete;
        IncompleteMinDurationSeconds = options.IncompleteMinDurationSeconds;
        IncompleteDurationUnit = options.IncompleteMinDurationSeconds > 0 && options.IncompleteMinDurationSeconds % 3600 == 0 ? "h" :
            options.IncompleteMinDurationSeconds % 60 == 0 ? "m" : "s";
        Overwrite = options.Overwrite;
        TimeoutSeconds = options.TimeoutSeconds;
        Reconnect = options.Reconnect;
        UserAgentMode = options.UserAgentMode;
        CustomUserAgent = options.CustomUserAgent;
        IpFamily = options.IpFamily;
        RelayEnabled = options.RelayEnabled;
        RelayPort = options.RelayPort;
        MaxRelayClients = options.MaxRelayClients;
        RelayBindAddress = options.RelayBindAddress;
        RecordDurationSeconds = options.RecordDurationSeconds;
        MaxMegabytes = options.MaxMegabytes;
        Id3V1 = options.Id3V1;
        Id3V2 = options.Id3V2;
        MetadataEncoding = options.MetadataEncoding;
        SilenceDetector = options.SilenceDetector;
        SplitOffsetMilliseconds = options.SplitOffsetMilliseconds;
        SplitPadding = options.SplitPadding;
        SplitSearchWindow = options.SplitSearchWindow;
        SplitSilenceMilliseconds = options.SplitSilenceMilliseconds;
        Proxy = options.Proxy;
        Http10 = options.Http10;
        TlsCaFile = options.TlsCaFile;
        InsecureTls = options.InsecureTls;
        HlsMode = options.HlsMode;
        HlsBitrate = options.HlsBitrate;
    }

    public Core.Models.RecordingOptions Snapshot()
    {
        var options = _original with
        {
            FilenamePattern = FilenamePattern,
            RecordingMode = RecordingMode,
            Wav = Wav,
            SkipInitialTracks = SkipInitialTracks,
            KeepIncomplete = KeepIncomplete,
            IncompleteMinDurationSeconds = IncompleteMinDurationSeconds,
            Overwrite = Overwrite,
            TimeoutSeconds = TimeoutSeconds,
            Reconnect = Reconnect,
            UserAgentMode = UserAgentMode,
            CustomUserAgent = CustomUserAgent,
            IpFamily = IpFamily,
            RelayEnabled = RelayEnabled,
            RelayPort = RelayPort,
            MaxRelayClients = MaxRelayClients,
            RelayBindAddress = RelayBindAddress,
            RecordDurationSeconds = RecordDurationSeconds,
            MaxMegabytes = MaxMegabytes,
            Id3V1 = Id3V1,
            Id3V2 = Id3V2,
            MetadataEncoding = MetadataEncoding,
            SilenceDetector = SilenceDetector,
            SplitOffsetMilliseconds = SplitOffsetMilliseconds,
            SplitPadding = SplitPadding,
            SplitSearchWindow = SplitSearchWindow,
            SplitSilenceMilliseconds = SplitSilenceMilliseconds,
            Proxy = Proxy,
            Http10 = Http10,
            TlsCaFile = TlsCaFile,
            InsecureTls = InsecureTls,
            HlsMode = HlsMode,
            HlsBitrate = HlsBitrate,
        };
        Core.Models.RecordingArguments.Validate(options);
        return options;
    }
}
