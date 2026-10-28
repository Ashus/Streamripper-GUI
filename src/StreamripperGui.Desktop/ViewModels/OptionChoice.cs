namespace StreamripperGui.Desktop.ViewModels;

public sealed record OptionChoice(string Value, string Label)
{
    public override string ToString() => Label;
}
