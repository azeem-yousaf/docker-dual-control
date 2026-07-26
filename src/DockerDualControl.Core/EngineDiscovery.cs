namespace DockerDualControl.Core;

public sealed record DiscoveredEngine(DockerEngine Engine, bool IsAvailable, string? Version, string? Os, bool IsInstalled);

public static class EngineDiscovery
{
    public static async Task<List<DiscoveredEngine>> DiscoverAsync(CancellationToken ct = default)
    {
        var probes = new List<Task<DiscoveredEngine>> { ProbeAsync(DockerEngine.Windows(), ct) };

        foreach (var distro in await ListWslDistrosAsync(ct))
            probes.Add(ProbeAsync(DockerEngine.ForWslDistro(distro), ct));

        return (await Task.WhenAll(probes)).ToList();
    }

    public static IEnumerable<string> FilterDistros(IEnumerable<string> raw)
    {
        foreach (var line in raw)
        {
            var name = line.Replace("\0", "").Trim();
            if (name.Length == 0)
                continue;
            if (name.StartsWith("docker-desktop", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return name;
        }
    }

    private static async Task<List<string>> ListWslDistrosAsync(CancellationToken ct)
    {
        try
        {
            var result = await ProcessRunner.RunAsync("wsl.exe", new[] { "-l", "-q" }, TimeSpan.FromSeconds(10), ct);
            if (result.ExitCode != 0)
                return new List<string>();
            return FilterDistros(result.StdOut.Split('\n')).ToList();
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            return new List<string>(); // WSL not installed or unresponsive
        }
    }

    private static async Task<DiscoveredEngine> ProbeAsync(DockerEngine engine, CancellationToken ct)
    {
        var ping = await new DockerService(engine).PingAsync(ct);
        var installed = ping is not null || await EngineControl.EngineExistsAsync(engine, ct);
        return new DiscoveredEngine(engine, ping is not null, ping?.Version, ping?.Os, installed);
    }
}
