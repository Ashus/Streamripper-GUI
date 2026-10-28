namespace StreamripperGui.Desktop.ViewModels;

public sealed class StationViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    public Core.Models.Station Data { get; private set; }
    public string Id => Data.Id;
    public string Name => Data.Name;
    public string Url => Data.Url;
    public ConsoleViewModel Console { get; } = new();
    public RecordingViewModel Recording { get; }
    public bool IsActive => Recording.IsActive;
    public string Status => Recording.Status;
    public string StreamDescription
    {
        get
        {
            var stream = Data.LastObservedStream;
            if (stream is null) return Localization.Localizer.Current.Format("StreamNotObserved");
            var description = Localization.Localizer.Current.Format("Format", stream["nativeFormat"]?.GetValue<string>() ?? "");
            if (stream["metadataRules"]?.GetValue<string>() == "unsupported")
                description += " · " + Localization.Localizer.Current.Format("RulesUnsupported");
            if (stream["trackTitles"]?.GetValue<string>() == "unavailable")
                description += " · " + Localization.Localizer.Current.Format("TitlesUnavailable");
            if (stream["audioFormat"]?.GetValue<string>() is { } audio && audio is not ("unknown" or "MP3"))
                description += " · " + Localization.Localizer.Current.Format("WavUnavailable");
            return description;
        }
    }

    public StationViewModel(Core.Models.Station data)
    {
        Data = data;
        Console.Title = Localization.Localizer.Current.Message("ConsoleStationTitle", data.Name);
        Recording = new RecordingViewModel(Console) { StationName = data.Name, Url = data.Url };
        Recording.PropertyChanged += RecordingChanged;
    }

    public void Update(Core.Models.Station data)
    {
        Data = data;
        Recording.StationName = data.Name;
        Recording.Url = data.Url;
        Console.Title = Localization.Localizer.Current.Message("ConsoleStationTitle", data.Name);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Url));
        OnPropertyChanged(nameof(StreamDescription));
    }

    private void RecordingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(RecordingViewModel.IsActive)) OnPropertyChanged(nameof(IsActive));
        if (args.PropertyName == nameof(RecordingViewModel.Status)) OnPropertyChanged(nameof(Status));
    }


    public void RefreshTranslations() => OnPropertyChanged(nameof(StreamDescription));

    public void Dispose()
    {
        Recording.PropertyChanged -= RecordingChanged;
        Console.Dispose();
    }
}
