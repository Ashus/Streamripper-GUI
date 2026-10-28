namespace StreamripperGui.Desktop.Tests;

public sealed class CompiledXamlTests
{
    [Xunit.Fact]
    public void DesktopAssemblyContainsCompiledXamlForApplicationAndViews() =>
        Services.CompiledXamlCheck.Verify(typeof(App).Assembly);

    [Xunit.Fact]
    public void AssemblyWithoutCompiledXamlIsRejected()
    {
        var error = Xunit.Assert.Throws<InvalidDataException>(() =>
            Services.CompiledXamlCheck.Verify(typeof(CompiledXamlTests).Assembly));
        Xunit.Assert.Contains("no compiled Avalonia XAML loader", error.Message);
    }
}
