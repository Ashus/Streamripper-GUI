namespace StreamripperGui.Desktop.Views;

public sealed class OptionLabel : Avalonia.Controls.UserControl
{
    public static readonly Avalonia.StyledProperty<string?> TextProperty = Avalonia.AvaloniaProperty.Register<OptionLabel, string?>(nameof(Text));
    private readonly Avalonia.Controls.TextBlock _label = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontWeight = Avalonia.Media.FontWeight.Normal };
    private readonly Avalonia.Controls.Documents.Run _marker = new() { FontWeight = Avalonia.Media.FontWeight.Bold };
    private bool _isChanged;

    public OptionLabel()
    {
        Content = _label;
        PropertyChanged += (_, args) =>
        {
            if (args.Property == TextProperty) UpdateText();
            if (args.Property.Name == nameof(ActualThemeVariant)) UpdateColor();
        };
        UpdateColor();
        UpdateText();
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; _marker.Text = value ? " *" : ""; }
    }

    private void UpdateText()
    {
        _label.Inlines!.Clear();
        _label.Inlines.Add(new Avalonia.Controls.Documents.Run(Text ?? ""));
        _label.Inlines.Add(_marker);
    }

    private void UpdateColor() => _marker.Foreground = Avalonia.Media.Brush.Parse(
        ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? "#FFBF69" : "#9C5700");
}
