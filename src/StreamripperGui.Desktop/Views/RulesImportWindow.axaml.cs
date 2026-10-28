namespace StreamripperGui.Desktop.Views;

public sealed partial class RulesImportWindow : Avalonia.Controls.Window
{
    public RulesImportWindow() => InitializeComponent();
    public RulesImportWindow(ViewModels.RulesImportViewModel model) : this()
    {
        DataContext = model;
        model.Applied += ImportApplied;
        Closed += (_, _) => { model.Applied -= ImportApplied; model.Dispose(); };
    }
    private void ImportApplied(object? sender, EventArgs args) => Close();
    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close();
}
