namespace StreamripperGui.Desktop;

public sealed partial class App : Avalonia.Application
{
    internal static Services.SingleInstance? Instance { get; set; }
    public override void Initialize() => Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Views.MainWindow { DataContext = new ViewModels.MainWindowViewModel() };
            desktop.MainWindow.Opened += (_, _) => Instance?.SetActivationHandler(() =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => (desktop.MainWindow as Views.MainWindow)?.BringToFront()));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
