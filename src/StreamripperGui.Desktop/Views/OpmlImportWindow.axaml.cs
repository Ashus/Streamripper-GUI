namespace StreamripperGui.Desktop.Views;

public sealed partial class OpmlImportWindow : Avalonia.Controls.Window
{
    public OpmlImportWindow() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    public OpmlImportWindow(ViewModels.OpmlImportViewModel model) : this()
    {
        DataContext = model;
        model.Applied += ImportApplied;
        Closed += (_, _) =>
        {
            model.Applied -= ImportApplied;
            model.Dispose();
        };
    }

    private void ImportApplied(object? sender, EventArgs args) => Close();
    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close();

}
