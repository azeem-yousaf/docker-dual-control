using DockerDualControl.Core.Models;

namespace DockerDualControl.Core;

public enum ContainerChangeKind
{
    Started,
    Stopped,
}

public sealed record ContainerStateChange(
    string EngineId,
    string ContainerId,
    string ContainerName,
    ContainerChangeKind Kind);

/// <summary>
/// Diffs successive container-list snapshots per engine into Started/Stopped events.
/// The first snapshot for an engine is a baseline and emits nothing; callers skip
/// engines whose listing failed, so an offline engine never reads as "everything
/// stopped" — its next successful snapshot diffs against the last-known one.
/// </summary>
public sealed class ContainerStateTracker
{
    private readonly Dictionary<string, Dictionary<string, (string Name, bool IsRunning)>> _snapshots = new();

    public IReadOnlyList<ContainerStateChange> Update(string engineId, IReadOnlyList<ContainerInfo> containers)
    {
        var fresh = new Dictionary<string, (string Name, bool IsRunning)>();
        foreach (var c in containers)
            fresh[c.Id] = (DisplayName(c), c.IsRunning);

        var hadBaseline = _snapshots.TryGetValue(engineId, out var previous);
        _snapshots[engineId] = fresh;
        if (!hadBaseline)
            return Array.Empty<ContainerStateChange>();

        var changes = new List<ContainerStateChange>();
        foreach (var (id, (name, isRunning)) in fresh)
        {
            var wasRunning = previous!.TryGetValue(id, out var old) && old.IsRunning;
            if (isRunning && !wasRunning)
                changes.Add(new ContainerStateChange(engineId, id, name, ContainerChangeKind.Started));
            else if (!isRunning && wasRunning)
                changes.Add(new ContainerStateChange(engineId, id, name, ContainerChangeKind.Stopped));
        }
        foreach (var (id, (name, wasRunning)) in previous!)
        {
            if (wasRunning && !fresh.ContainsKey(id))
                changes.Add(new ContainerStateChange(engineId, id, name, ContainerChangeKind.Stopped));
        }
        return changes;
    }

    private static string DisplayName(ContainerInfo c) =>
        !string.IsNullOrWhiteSpace(c.Names) ? c.Names
        : c.Id.Length > 12 ? c.Id[..12]
        : c.Id;
}
