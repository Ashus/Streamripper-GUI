namespace StreamripperGui.Desktop.Views;

public sealed partial class DeleteConfirmationWindow : Avalonia.Controls.Window
{
    public DeleteConfirmationWindow() => InitializeComponent();

    public DeleteConfirmationWindow(ViewModels.CatalogDeletionRequest request) : this()
    {
        DataContext = request;
        Height = request.HasStations ? 440 : 280;
    }

    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(false);
    private void DeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(true);
}
