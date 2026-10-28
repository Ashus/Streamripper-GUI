namespace StreamripperGui.Desktop.Tests;

public sealed class LinuxIntegrationTests
{
    public static bool IsLinux => OperatingSystem.IsLinux();

    [Xunit.Fact(Skip = "Requires a Linux shell.", SkipUnless = nameof(IsLinux))]
    public async Task CommandPreviewQuotingPreservesShellMetacharacters()
    {
        const string value = "space ' $HOME $(printf expanded) ; | \\quotes";
        var cancellation = Xunit.TestContext.Current.CancellationToken;
        using var process = Services.RecordingProcess.Start("/bin/sh", ["-c", "printf '%s' " + Services.RecordingProcess.QuoteArgument(value)]);
        using var reader = new StreamReader(process.StandardOutput);
        var output = await reader.ReadToEndAsync(cancellation).WaitAsync(TimeSpan.FromSeconds(10), cancellation);
        await process.WaitForExitAsync();
        Xunit.Assert.Equal(0, process.ExitCode);
        Xunit.Assert.Equal(value, output);
    }

    [Xunit.Fact(Skip = "Requires Linux and system GLib/Python.", SkipUnless = nameof(IsLinux))]
    public async Task RecordersPreserveArgumentsAndStopIndependently()
    {
        var cancellation = Xunit.TestContext.Current.CancellationToken;
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "signal_recorder.py");
        const string argument = "Žluťoučký / path with spaces; $HOME \"quotes\"";
        using var first = Services.RecordingProcess.Start("/usr/bin/python3", [fixture, argument]);
        using var second = Services.RecordingProcess.Start("/usr/bin/python3", [fixture, "second"]);
        using var output = new StreamReader(first.StandardOutput);
        using var error = new StreamReader(first.StandardError);
        using var secondOutput = new StreamReader(second.StandardOutput);
        try
        {
            Xunit.Assert.Equal(argument, await output.ReadLineAsync(cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(10), cancellation));
            Xunit.Assert.Equal("diagnostic", await error.ReadLineAsync(cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(10), cancellation));
            Xunit.Assert.Equal("second", await secondOutput.ReadLineAsync(cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(10), cancellation));
            await first.RequestStopAsync();
            await first.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10), cancellation);
            Xunit.Assert.Equal(0, first.ExitCode);
            Xunit.Assert.Equal("stopped", await output.ReadLineAsync(cancellation));
            Xunit.Assert.False(second.HasExited);
            await first.RequestStopAsync();
            second.ForceStop();
            await second.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10), cancellation);
            Xunit.Assert.NotEqual(0, second.ExitCode);
        }
        finally
        {
            first.ForceStop();
            second.ForceStop();
            await Task.WhenAll(first.WaitForExitAsync(), second.WaitForExitAsync());
        }
    }

    [Xunit.Fact(Skip = "Requires Linux and system GLib/Python.", SkipUnless = nameof(IsLinux))]
    public async Task FastExitRetainsItsExitCode()
    {
        using var process = Services.RecordingProcess.Start("/bin/false", []);
        await process.WaitForExitAsync();
        Xunit.Assert.True(process.HasExited);
        Xunit.Assert.Equal(1, process.ExitCode);
        await process.RequestStopAsync();
    }

    [Xunit.Fact(Skip = "Requires Linux and system GLib/Python.", SkipUnless = nameof(IsLinux))]
    public async Task SingleInstanceLockAndActivationSurviveRestart()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "instance-tests", Guid.NewGuid().ToString("N"));
        try
        {
            using (var owner = Services.SingleInstance.TryAcquire(directory))
            {
                Xunit.Assert.NotNull(owner);
                var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                owner.SetActivationHandler(() => activated.TrySetResult());
                Xunit.Assert.Null(Services.SingleInstance.TryAcquire(directory));
                Xunit.Assert.True(await Services.SingleInstance.ActivateExistingAsync(directory));
                await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
            }

            using var next = Services.SingleInstance.TryAcquire(directory);
            Xunit.Assert.NotNull(next);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Xunit.Fact(Skip = "Requires Linux and system GLib/Python.", SkipUnless = nameof(IsLinux))]
    public void SystemGlibCompilesNativeRegexesAndRejectsInvalidOnes()
    {
        using var validator = new Infrastructure.GlibRegexValidator("/usr/bin/streamripper");
        Xunit.Assert.Null(validator.Check(@"^(artist) - (title)$", true).Error);
        Xunit.Assert.Equal(2, validator.Check(@"^(artist) - (title)$", true).CaptureCount);
        Xunit.Assert.NotNull(validator.Check("[invalid", false).Error);
        Xunit.Assert.StartsWith("/", Infrastructure.PlatformPaths.ConfigurationDirectory);
        Xunit.Assert.StartsWith("/", Infrastructure.PlatformPaths.RecordingDirectory);
        Xunit.Assert.Equal("/usr/bin/streamripper", Infrastructure.PlatformPaths.StreamripperExecutable);
    }
}