namespace DockerDualControl.Core;

/// <summary>Container counts for one reachable engine, as fed to the tray tooltip.</summary>
public sealed record EngineContainerSummary(string EngineName, int Running, int Stopped);

/// <summary>
/// Formats the tray icon hover tooltip. NotifyIcon.Text throws beyond 127 characters,
/// so the output is guaranteed to fit: the per-engine breakdown line is dropped first,
/// then (defensively) the text is hard-truncated.
/// </summary>
public static class TrayStatusFormatter
{
    public const int MaxTooltipLength = 127;

    private const string Header = "Docker Dual Control";

    public static string Format(IReadOnlyList<EngineContainerSummary> engines)
    {
        if (engines.Count == 0)
            return $"{Header}\nNo engines reachable";

        var running = engines.Sum(e => e.Running);
        var stopped = engines.Sum(e => e.Stopped);
        if (running == 0 && stopped == 0)
            return $"{Header}\nNo containers";

        var totalParts = new List<string>(2);
        if (running > 0)
            totalParts.Add($"● {running} running");
        if (stopped > 0)
            totalParts.Add($"○ {stopped} stopped");
        var text = $"{Header}\n{string.Join(" · ", totalParts)}";

        // With one engine the breakdown would just repeat the totals line.
        if (engines.Count > 1)
        {
            var breakdown = string.Join(" · ",
                engines.Select(e => $"{e.EngineName} {e.Running}/{e.Running + e.Stopped}"));
            if (text.Length + 1 + breakdown.Length <= MaxTooltipLength)
                text = $"{text}\n{breakdown}";
        }

        return text.Length <= MaxTooltipLength ? text : text[..(MaxTooltipLength - 1)] + "…";
    }
}
