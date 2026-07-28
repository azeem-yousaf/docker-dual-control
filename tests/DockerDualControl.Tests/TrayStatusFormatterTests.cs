using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class TrayStatusFormatterTests
{
    private const string Header = "Docker Dual Control";

    [Fact]
    public void NoEngines_ReportsNoEnginesReachable()
    {
        var text = TrayStatusFormatter.Format(Array.Empty<EngineContainerSummary>());

        Assert.Equal($"{Header}\nNo engines reachable", text);
    }

    [Fact]
    public void EnginesWithoutContainers_ReportsNoContainers()
    {
        var text = TrayStatusFormatter.Format(new[]
        {
            new EngineContainerSummary("Windows", Running: 0, Stopped: 0),
            new EngineContainerSummary("Ubuntu", Running: 0, Stopped: 0),
        });

        Assert.Equal($"{Header}\nNo containers", text);
    }

    [Fact]
    public void SingleEngine_MixedCounts_ShowsBothSides_NoBreakdownLine()
    {
        var text = TrayStatusFormatter.Format(new[]
        {
            new EngineContainerSummary("Windows", Running: 3, Stopped: 1),
        });

        Assert.Equal($"{Header}\n● 3 running · ○ 1 stopped", text);
    }

    [Fact]
    public void AllRunning_OmitsStoppedSide()
    {
        var text = TrayStatusFormatter.Format(new[]
        {
            new EngineContainerSummary("Windows", Running: 4, Stopped: 0),
        });

        Assert.Equal($"{Header}\n● 4 running", text);
    }

    [Fact]
    public void AllStopped_OmitsRunningSide()
    {
        var text = TrayStatusFormatter.Format(new[]
        {
            new EngineContainerSummary("Ubuntu", Running: 0, Stopped: 2),
        });

        Assert.Equal($"{Header}\n○ 2 stopped", text);
    }

    [Fact]
    public void MultipleEngines_AddsPerEngineBreakdown()
    {
        var text = TrayStatusFormatter.Format(new[]
        {
            new EngineContainerSummary("Windows", Running: 2, Stopped: 1),
            new EngineContainerSummary("Ubuntu", Running: 1, Stopped: 0),
        });

        Assert.Equal(
            $"{Header}\n● 3 running · ○ 1 stopped\nWindows 2/3 · Ubuntu 1/1",
            text);
    }

    [Fact]
    public void BreakdownTooLong_DropsBreakdownLineButKeepsTotals()
    {
        var engines = Enumerable.Range(1, 6)
            .Select(i => new EngineContainerSummary($"very-long-distro-name-number-{i}", Running: 1, Stopped: 1))
            .ToList();

        var text = TrayStatusFormatter.Format(engines);

        Assert.Equal($"{Header}\n● 6 running · ○ 6 stopped", text);
        Assert.True(text.Length <= TrayStatusFormatter.MaxTooltipLength);
    }

    [Fact]
    public void OutputNeverExceedsNotifyIconLimit()
    {
        var engines = Enumerable.Range(1, 40)
            .Select(i => new EngineContainerSummary(new string('x', 60) + i, Running: 123456, Stopped: 654321))
            .ToList();

        var text = TrayStatusFormatter.Format(engines);

        Assert.True(text.Length <= TrayStatusFormatter.MaxTooltipLength);
        Assert.StartsWith(Header, text);
    }
}
