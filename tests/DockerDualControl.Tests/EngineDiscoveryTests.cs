using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class EngineDiscoveryTests
{
    [Fact]
    public void FilterDistros_StripsNullChars_SkipsDockerDesktopAndBlanks()
    {
        // wsl.exe -l -q emits UTF-16; when read as UTF-8 every other byte is '\0'.
        var raw = new[]
        {
            "U\0b\0u\0n\0t\0u\0-\02\04\0.\00\04\0",
            "docker-desktop",
            "docker-desktop-data",
            "",
            "  ",
            "Debian",
        };

        var distros = EngineDiscovery.FilterDistros(raw).ToList();

        Assert.Equal(new[] { "Ubuntu-24.04", "Debian" }, distros);
    }
}
