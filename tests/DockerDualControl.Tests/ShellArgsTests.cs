using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class ShellArgsTests
{
    private const string LinuxShellFallback =
        "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

    [Fact]
    public void BuildShellArgs_LinuxEngine_PrefersBashFallsBackToSh()
    {
        var args = DockerService.BuildShellArgs("abc123", "linux");

        Assert.Equal(new[] { "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback }, args);
    }

    [Fact]
    public void BuildShellArgs_UnknownOs_TreatedAsLinux()
    {
        var args = DockerService.BuildShellArgs("abc123", null);

        Assert.Equal(new[] { "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback }, args);
    }

    [Fact]
    public void BuildShellArgs_WindowsContainers_UseCmd()
    {
        var args = DockerService.BuildShellArgs("abc123", "windows");

        Assert.Equal(new[] { "exec", "-it", "abc123", "cmd" }, args);
    }

    [Fact]
    public void ShellArgs_ComposeThroughWslEngine_Verbatim()
    {
        // The sh -c script contains ; and > — `wsl -e` must carry it as one arg.
        var engine = DockerEngine.ForWslDistro("Ubuntu-24.04");

        var (fileName, args) = engine.BuildCommand(DockerService.BuildShellArgs("abc123", "linux"));

        Assert.Equal("wsl.exe", fileName);
        Assert.Equal(new[]
        {
            "-d", "Ubuntu-24.04", "-e", "docker",
            "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback,
        }, args);
    }
}
