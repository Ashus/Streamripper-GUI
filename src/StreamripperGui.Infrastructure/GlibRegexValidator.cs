namespace StreamripperGui.Infrastructure;

public sealed class GlibRegexValidator : Core.Models.INativeRegexValidator, IDisposable
{
    private readonly nint _library;
    private readonly RegexNew _create;
    private readonly RegexUnref _unref;
    private readonly CaptureCount _captures;
    private readonly ErrorFree _errorFree;

    public GlibRegexValidator(string streamripperExecutable)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(streamripperExecutable))!;
        var filename = OperatingSystem.IsWindows() ? "libglib-2.0-0.dll" : "libglib-2.0.so.0";
        var path = Path.Combine(directory, filename);
        _library = OperatingSystem.IsWindows()
            ? System.Runtime.InteropServices.NativeLibrary.Load(path, typeof(GlibRegexValidator).Assembly,
                System.Runtime.InteropServices.DllImportSearchPath.UseDllDirectoryForDependencies |
                System.Runtime.InteropServices.DllImportSearchPath.SafeDirectories)
            : System.Runtime.InteropServices.NativeLibrary.Load(File.Exists(path) ? path : filename);
        try
        {
            _create = Export<RegexNew>("g_regex_new");
            _unref = Export<RegexUnref>("g_regex_unref");
            _captures = Export<CaptureCount>("g_regex_get_capture_count");
            _errorFree = Export<ErrorFree>("g_error_free");
        }
        catch
        {
            System.Runtime.InteropServices.NativeLibrary.Free(_library);
            throw;
        }
    }

    private T Export<T>(string name) where T : Delegate =>
        System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(
            System.Runtime.InteropServices.NativeLibrary.GetExport(_library, name));

    public Core.Models.RegexCheck Check(string pattern, bool ignoreCase)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(pattern + "\0");
        var regex = _create(bytes, ignoreCase ? 1 : 0, 0, out var error);
        try
        {
            if (regex != 0) return new(null, _captures(regex));
            var message = error == 0 ? "GLib rejected the regular expression." :
                System.Runtime.InteropServices.Marshal.PtrToStringUTF8(
                    System.Runtime.InteropServices.Marshal.ReadIntPtr(error, 8)) ?? "Invalid regular expression.";
            return new(message);
        }
        finally
        {
            if (regex != 0) _unref(regex);
            if (error != 0) _errorFree(error);
        }
    }

    public void Dispose() => System.Runtime.InteropServices.NativeLibrary.Free(_library);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate nint RegexNew(byte[] pattern, int flags, int matchFlags, out nint error);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate void RegexUnref(nint regex);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate int CaptureCount(nint regex);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
    private delegate void ErrorFree(nint error);
}
