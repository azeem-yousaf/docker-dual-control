using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesStdOutAndExitCode()
    {
        var result = await ProcessRunner.RunAsync("cmd", new[] { "/c", "echo hi" }, TimeSpan.FromSeconds(10));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hi", result.StdOut.Trim());
    }

    [Fact]
    public async Task RunAsync_NonZeroExit_DoesNotThrow()
    {
        var result = await ProcessRunner.RunAsync("cmd", new[] { "/c", "exit 3" }, TimeSpan.FromSeconds(10));

        Assert.Equal(3, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_Timeout_Throws()
    {
        await Assert.ThrowsAsync<TimeoutException>(() =>
            ProcessRunner.RunAsync("cmd", new[] { "/c", "ping -n 30 127.0.0.1 >nul" }, TimeSpan.FromMilliseconds(300)));
    }
}
