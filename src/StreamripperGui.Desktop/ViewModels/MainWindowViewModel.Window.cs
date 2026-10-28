namespace StreamripperGui.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    public (double Width, double Height, bool Maximized, double CenterXPercent, double CenterYPercent, double ConsoleHeight) ReadWindowPreferences()
    {
        var window = _configuration["application"]?["window"] as System.Text.Json.Nodes.JsonObject;
        return (ReadDimension(window?["width"], 1130), ReadDimension(window?["height"], 840),
            window?["maximized"] is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<bool>(out var maximized) && maximized,
            ReadPercentage(window?["centerXPercent"]), ReadPercentage(window?["centerYPercent"]), ReadDimension(window?["consoleHeight"], 0));
    }

    private static double ReadDimension(System.Text.Json.Nodes.JsonNode? node, double fallback) =>
        node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<double>(out var number) &&
        double.IsFinite(number) && number > 0 && number <= 32768 ? number : fallback;

    private static double ReadPercentage(System.Text.Json.Nodes.JsonNode? node) =>
        node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<double>(out var number) &&
        double.IsFinite(number) && number >= 0 && number <= 100 ? number : 50;

    public void SaveWindowPreferences(double width, double height, bool maximized, double centerXPercent, double centerYPercent, double consoleHeight)
    {
        if (_loadFailed) return;
        try
        {
            var copy = _configuration.DeepClone().AsObject();
            var application = copy["application"]!.AsObject();
            var window = application["window"] as System.Text.Json.Nodes.JsonObject ?? new();
            window["width"] = width;
            window["height"] = height;
            window["maximized"] = maximized;
            window["centerXPercent"] = centerXPercent;
            window["centerYPercent"] = centerYPercent;
            if (double.IsFinite(consoleHeight) && consoleHeight >= 90) window["consoleHeight"] = consoleHeight;
            if (application["window"] != window) application["window"] = window;
            _store.SaveConfiguration(copy);
            _configuration = copy;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Saving window geometry failed: {exception}");
        }
    }
}
