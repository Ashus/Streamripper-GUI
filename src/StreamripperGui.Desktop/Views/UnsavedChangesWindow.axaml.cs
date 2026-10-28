namespace StreamripperGui.Desktop.Views;

public sealed partial class UnsavedChangesWindow : Avalonia.Controls.Window
{
    public UnsavedChangesWindow() => InitializeComponent();

    public UnsavedChangesWindow(string name) : this() =>
        Question.Text = Localization.Localizer.Current.Format("UnsavedChangesQuestion", name);

    private void SaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(ViewModels.UnsavedChangesChoice.Save);
    private void DiscardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(ViewModels.UnsavedChangesChoice.Discard);
    private void CancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => Close(ViewModels.UnsavedChangesChoice.Cancel);
}