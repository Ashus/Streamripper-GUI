namespace StreamripperGui.Desktop.ViewModels;

internal static class CatalogOrder
{
    public static void Sort<T>(System.Collections.ObjectModel.ObservableCollection<T> items,
        Func<T, string?> id, Func<T, string> name)
    {
        var sorted = items.OrderBy(item => id(item) is null ? 0 : 1)
            .ThenBy(name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(id, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < sorted.Length; index++)
        {
            var previous = items.IndexOf(sorted[index]);
            if (previous != index) items.Move(previous, index);
        }
    }
}
