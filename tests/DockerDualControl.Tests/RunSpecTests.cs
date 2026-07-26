using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class RunSpecTests
{
    [Fact]
    public void ToDockerArgs_ImageOnly_RunsDetached()
    {
        var spec = new RunSpec { Image = "nginx:latest" };

        Assert.Equal(new[] { "run", "-d", "nginx:latest" }, spec.ToDockerArgs());
    }

    [Fact]
    public void ToDockerArgs_FullSpec_EmitsAllOptionsBeforeImage()
    {
        var spec = new RunSpec
        {
            Image = "nginx:latest",
            Name = "web",
            Ports = { new PortMapping("8080", "80"), new PortMapping("8443", "443") },
            Env = { new EnvVar("MODE", "prod") },
            Volumes = { new VolumeMapping("/data", "/usr/share/nginx/html") },
            Command = "nginx -g 'daemon off;'",
        };

        Assert.Equal(new[]
        {
            "run", "-d",
            "--name", "web",
            "-p", "8080:80",
            "-p", "8443:443",
            "-e", "MODE=prod",
            "-v", "/data:/usr/share/nginx/html",
            "nginx:latest",
            "nginx", "-g", "'daemon off;'",
        }, spec.ToDockerArgs());
    }

    [Fact]
    public void ToDockerArgs_BlankOptionalFields_AreOmitted()
    {
        var spec = new RunSpec { Image = "redis", Name = "  ", Command = "" };

        Assert.Equal(new[] { "run", "-d", "redis" }, spec.ToDockerArgs());
    }
}
