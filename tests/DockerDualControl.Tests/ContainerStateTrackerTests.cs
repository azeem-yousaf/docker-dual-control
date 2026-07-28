using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.Tests;

public class ContainerStateTrackerTests
{
    private static ContainerInfo Container(string id, string name, string state) =>
        new() { Id = id, Names = name, State = state };

    [Fact]
    public void FirstSnapshot_EmitsNothing()
    {
        var tracker = new ContainerStateTracker();

        var changes = tracker.Update("windows", new[]
        {
            Container("aaa", "web", "running"),
            Container("bbb", "db", "exited"),
        });

        Assert.Empty(changes);
    }

    [Fact]
    public void RunningToExited_EmitsStopped()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "exited") });

        var change = Assert.Single(changes);
        Assert.Equal(ContainerChangeKind.Stopped, change.Kind);
        Assert.Equal("windows", change.EngineId);
        Assert.Equal("aaa", change.ContainerId);
        Assert.Equal("web", change.ContainerName);
    }

    [Fact]
    public void ExitedToRunning_EmitsStarted()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "exited") });

        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        var change = Assert.Single(changes);
        Assert.Equal(ContainerChangeKind.Started, change.Kind);
    }

    [Fact]
    public void NewContainerAlreadyRunning_EmitsStarted()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", Array.Empty<ContainerInfo>());

        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        var change = Assert.Single(changes);
        Assert.Equal(ContainerChangeKind.Started, change.Kind);
    }

    [Fact]
    public void NewContainerNotRunning_EmitsNothing()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", Array.Empty<ContainerInfo>());

        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "created") });

        Assert.Empty(changes);
    }

    [Fact]
    public void RunningContainerDisappears_EmitsStopped()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        var changes = tracker.Update("windows", Array.Empty<ContainerInfo>());

        var change = Assert.Single(changes);
        Assert.Equal(ContainerChangeKind.Stopped, change.Kind);
        Assert.Equal("web", change.ContainerName);
    }

    [Fact]
    public void ExitedContainerDisappears_EmitsNothing()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "exited") });

        var changes = tracker.Update("windows", Array.Empty<ContainerInfo>());

        Assert.Empty(changes);
    }

    [Fact]
    public void UnchangedSnapshot_EmitsNothing()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        Assert.Empty(changes);
    }

    [Fact]
    public void EnginesAreIndependent()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        // First snapshot for the WSL engine is a baseline even though the
        // tracker has already seen the Windows engine.
        var wslChanges = tracker.Update("wsl:Ubuntu", new[] { Container("aaa", "web", "running") });
        Assert.Empty(wslChanges);

        // Stopping on one engine emits only for that engine.
        var changes = tracker.Update("wsl:Ubuntu", new[] { Container("aaa", "web", "exited") });
        var change = Assert.Single(changes);
        Assert.Equal("wsl:Ubuntu", change.EngineId);
    }

    [Fact]
    public void SkippedTick_DiffsAgainstLastKnownSnapshot()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("aaa", "web", "running") });

        // Engine offline for a tick: no Update call. When it returns, the diff
        // is against the last-known snapshot.
        var changes = tracker.Update("windows", new[] { Container("aaa", "web", "exited") });

        var change = Assert.Single(changes);
        Assert.Equal(ContainerChangeKind.Stopped, change.Kind);
    }

    [Fact]
    public void MultipleChangesInOneTick_AllEmitted()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[]
        {
            Container("aaa", "web", "running"),
            Container("bbb", "db", "exited"),
        });

        var changes = tracker.Update("windows", new[]
        {
            Container("aaa", "web", "exited"),
            Container("bbb", "db", "running"),
        });

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.ContainerName == "web" && c.Kind == ContainerChangeKind.Stopped);
        Assert.Contains(changes, c => c.ContainerName == "db" && c.Kind == ContainerChangeKind.Started);
    }

    [Fact]
    public void EmptyName_FallsBackToShortId()
    {
        var tracker = new ContainerStateTracker();
        tracker.Update("windows", new[] { Container("a1b2c3d4e5f6789", "", "running") });

        var changes = tracker.Update("windows", new[] { Container("a1b2c3d4e5f6789", "", "exited") });

        var change = Assert.Single(changes);
        Assert.Equal("a1b2c3d4e5f6", change.ContainerName);
    }
}
