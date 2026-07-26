using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class DockerEngineTests
{
    [Fact]
    public void WindowsEngine_BuildsNamedPipeCommand()
    {
        var engine = DockerEngine.Windows();

        var (fileName, args) = engine.BuildCommand(new[] { "ps", "-a" });

        Assert.Equal("docker", fileName);
        Assert.Equal(new[] { "-H", "npipe:////./pipe/docker_engine", "ps", "-a" }, args);
    }

    [Fact]
    public void WslEngine_BuildsWslCommand()
    {
        var engine = DockerEngine.ForWslDistro("Ubuntu-24.04");

        var (fileName, args) = engine.BuildCommand(new[] { "ps", "-a" });

        Assert.Equal("wsl.exe", fileName);
        Assert.Equal(new[] { "-d", "Ubuntu-24.04", "-e", "docker", "ps", "-a" }, args);
    }

    [Fact]
    public void WslEngine_ExecsDirectly_SoShellCharactersPassThroughVerbatim()
    {
        // Without `wsl -e`, args are joined and run through the distro's shell,
        // so a format string like {{.Server.Version}}|{{.Server.Os}} becomes a pipe.
        var engine = DockerEngine.ForWslDistro("Ubuntu-24.04");

        var (_, args) = engine.BuildCommand(
            new[] { "version", "--format", "{{.Server.Version}}|{{.Server.Os}}" });

        Assert.Equal("-e", args[2]);
        Assert.Contains("{{.Server.Version}}|{{.Server.Os}}", args);
    }

    [Fact]
    public void Engines_HaveDistinctIdsAndFriendlyNames()
    {
        var win = DockerEngine.Windows();
        var wsl = DockerEngine.ForWslDistro("Ubuntu-24.04");

        Assert.NotEqual(win.Id, wsl.Id);
        Assert.Equal(EngineKind.Windows, win.Kind);
        Assert.Equal(EngineKind.Wsl, wsl.Kind);
        Assert.Contains("Ubuntu-24.04", wsl.DisplayName);
    }
}
