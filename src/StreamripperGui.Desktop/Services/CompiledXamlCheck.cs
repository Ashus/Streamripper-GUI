namespace StreamripperGui.Desktop.Services;

public static class CompiledXamlCheck
{
    public static void Verify(System.Reflection.Assembly assembly)
    {
        if (assembly.GetType("CompiledAvaloniaXaml.!XamlLoader") is null)
            throw new InvalidDataException("The assembly has no compiled Avalonia XAML loader. Run a complete build.");

        var roots = assembly.DefinedTypes.Where(type => type.AsType() == typeof(App) ||
                                                        type.Namespace == "StreamripperGui.Desktop.Views" && type.DeclaredMethods.Any(method => method.Name == "InitializeComponent"));
        foreach (var root in roots)
        {
            // Avalonia injects this method when compiling an x:Class resource.
            if (!root.DeclaredMethods.Any(method => method.Name == "!XamlIlPopulate"))
                throw new InvalidDataException($"Compiled Avalonia XAML is missing for {root.FullName}. Run a complete build.");
        }
    }
}