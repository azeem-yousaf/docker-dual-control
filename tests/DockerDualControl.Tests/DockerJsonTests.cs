using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.Tests;

public class DockerJsonTests
{
    // Real shape emitted by: docker ps -a --format "{{json .}}"
    private const string PsLine =
        """{"Command":"\"/hello\"","CreatedAt":"2026-07-25 10:12:31 +0100 BST","ID":"a1b2c3d4e5f6","Image":"hello-world","Labels":"","LocalVolumes":"0","Mounts":"","Names":"epic_swan","Networks":"bridge","Ports":"0.0.0.0:8080->80/tcp","RunningFor":"2 hours ago","Size":"0B","State":"running","Status":"Up 2 hours"}""";

    // Real shape emitted by: docker images --format "{{json .}}"
    private const string ImageLine =
        """{"Containers":"N/A","CreatedAt":"2026-07-20 09:00:00 +0100 BST","CreatedSince":"5 days ago","Digest":"<none>","ID":"sha256:abcd1234","Repository":"nginx","SharedSize":"N/A","Size":"187MB","Tag":"latest","UniqueSize":"N/A","VirtualSize":"187MB"}""";

    [Fact]
    public void ParseLines_Containers_MapsFields()
    {
        var list = DockerJson.ParseLines<ContainerInfo>(PsLine + "\n\n" + PsLine + "\n");

        Assert.Equal(2, list.Count);
        var c = list[0];
        Assert.Equal("a1b2c3d4e5f6", c.Id);
        Assert.Equal("epic_swan", c.Names);
        Assert.Equal("hello-world", c.Image);
        Assert.Equal("running", c.State);
        Assert.Equal("Up 2 hours", c.Status);
        Assert.Equal("0.0.0.0:8080->80/tcp", c.Ports);
        Assert.True(c.IsRunning);
    }

    [Fact]
    public void ParseLines_Images_MapsFields()
    {
        var list = DockerJson.ParseLines<ImageInfo>(ImageLine);

        var i = Assert.Single(list);
        Assert.Equal("sha256:abcd1234", i.Id);
        Assert.Equal("nginx", i.Repository);
        Assert.Equal("latest", i.Tag);
        Assert.Equal("187MB", i.Size);
        Assert.Equal("5 days ago", i.CreatedSince);
    }

    [Fact]
    public void ParseLines_EmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(DockerJson.ParseLines<ContainerInfo>(""));
        Assert.Empty(DockerJson.ParseLines<ContainerInfo>("   \r\n  \n"));
    }

    [Fact]
    public void ContainerInfo_ExitedState_IsNotRunning()
    {
        var list = DockerJson.ParseLines<ContainerInfo>(PsLine.Replace("\"State\":\"running\"", "\"State\":\"exited\""));
        Assert.False(list[0].IsRunning);
    }
}
