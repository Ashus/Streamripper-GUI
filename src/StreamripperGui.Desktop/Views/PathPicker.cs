namespace StreamripperGui.Desktop.Views;

internal static class PathPicker
{
    public static async Task<string?> ChooseAsync(Avalonia.Controls.TopLevel owner, string current, string title,
        bool folder = false, IReadOnlyList<Avalonia.Platform.Storage.FilePickerFileType>? types = null)
    {
        var provider = owner.StorageProvider;
        string absolute;
        try { absolute = string.IsNullOrWhiteSpace(current) ? AppContext.BaseDirectory : Path.GetFullPath(current, AppContext.BaseDirectory); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { absolute = AppContext.BaseDirectory; }
        var directory = folder ? absolute : Path.GetDirectoryName(absolute);
        while (directory is not null && !Directory.Exists(directory)) directory = Path.GetDirectoryName(directory);
        var start = directory is null ? null : await provider.TryGetFolderFromPathAsync(new Uri(directory));
        if (folder)
        {
            var folders = await provider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            { Title = title, AllowMultiple = false, SuggestedStartLocation = start });
            if (folders.Count == 0) return null;
            using var selected = folders[0];
            return LocalPath(selected);
        }
        var files = await provider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title, AllowMultiple = false, SuggestedStartLocation = start,
            SuggestedFileName = Path.GetFileName(absolute), FileTypeFilter = types
        });
        if (files.Count == 0) return null;
        using var file = files[0];
        return LocalPath(file);
    }

    private static string LocalPath(Avalonia.Platform.Storage.IStorageItem item) =>
        item.Path.IsFile ? item.Path.LocalPath : throw new IOException(Localization.Localizer.Current.Message("NonLocalPath"));
}
