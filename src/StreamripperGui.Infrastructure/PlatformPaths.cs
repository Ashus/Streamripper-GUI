namespace StreamripperGui.Infrastructure;

public static class PlatformPaths
{
    public static string StreamripperExecutable => OperatingSystem.IsWindows()
        ? "streamripper/streamripper.exe"
        : "/usr/bin/streamripper";

    public static string RecordingDirectory
    {
        get
        {
            var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            if (string.IsNullOrWhiteSpace(music))
                music = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music");
            return Path.Combine(music, "Streamripper");
        }
    }

    public static string ConfigurationDirectory => Path.Combine(Environment.GetFolderPath(
        OperatingSystem.IsWindows() ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData), "StreamripperGui");
}