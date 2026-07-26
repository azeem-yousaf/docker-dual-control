using System.Diagnostics;
using DockerDualControl.Core.Models;

namespace DockerDualControl.Core;

/// <summary>A live engine's server version and OS ("linux" or "windows").</summary>
public sealed record EnginePing(string Version, string? Os);

public sealed class DockerService
{
    private static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LongTimeout = TimeSpan.FromSeconds(120);

    public DockerEngine Engine { get; }

    public DockerService(DockerEngine engine) => Engine = engine;

    public async Task<List<ContainerInfo>> ListContainersAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(new[] { "ps", "-a", "--format", "{{json .}}" }, ListTimeout, ct);
        return DockerJson.ParseLines<ContainerInfo>(result.StdOut);
    }

    public async Task<List<ImageInfo>> ListImagesAsync(CancellationToken ct = default)
    {
        var result = await RunAsync(new[] { "images", "--format", "{{json .}}" }, ListTimeout, ct);
        return DockerJson.ParseLines<ImageInfo>(result.StdOut);
    }

    public Task StartContainerAsync(string id, CancellationToken ct = default) =>
        RunAsync(new[] { "start", id }, ListTimeout, ct);

    public Task StopContainerAsync(string id, CancellationToken ct = default) =>
        RunAsync(new[] { "stop", id }, LongTimeout, ct);

    public Task RestartContainerAsync(string id, CancellationToken ct = default) =>
        RunAsync(new[] { "restart", id }, LongTimeout, ct);

    public Task RemoveContainerAsync(string id, CancellationToken ct = default) =>
        RunAsync(new[] { "rm", "-f", id }, LongTimeout, ct);

    public Task RemoveImageAsync(string id, CancellationToken ct = default) =>
        RunAsync(new[] { "rmi", id }, LongTimeout, ct);

    public Task PullImageAsync(string reference, CancellationToken ct = default) =>
        RunAsync(new[] { "pull", reference }, TimeSpan.FromMinutes(15), ct);

    public async Task<string> RunContainerAsync(RunSpec spec, CancellationToken ct = default)
    {
        var result = await RunAsync(spec.ToDockerArgs(), LongTimeout, ct);
        return result.StdOut.Trim();
    }

    /// <summary>Returns the server version and OS, or null when the engine is unreachable.</summary>
    public async Task<EnginePing?> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var (fileName, args) = Engine.BuildCommand(
                new[] { "version", "--format", "{{.Server.Version}}|{{.Server.Os}}" });
            var result = await ProcessRunner.RunAsync(fileName, args, TimeSpan.FromSeconds(10), ct);
            if (result.ExitCode != 0)
                return null;
            var parts = result.StdOut.Trim().Split('|');
            var version = parts[0].Trim();
            if (version.Length == 0)
                return null;
            var os = parts.Length > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : null;
            return new EnginePing(version, os);
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>Starts `docker logs -f` for streaming; caller owns the returned process.</summary>
    public Process StartLogsProcess(string id)
    {
        var (fileName, args) = Engine.BuildCommand(new[] { "logs", "-f", "--tail", "500", id });
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
        var process = new Process { StartInfo = startInfo };
        process.Start();
        return process;
    }

    /// <summary>Docker args for an interactive shell: bash-or-sh on Linux, cmd on Windows containers.</summary>
    public static IReadOnlyList<string> BuildShellArgs(string id, string? serverOs) =>
        serverOs == "windows"
            ? new[] { "exec", "-it", id, "cmd" }
            : new[] { "exec", "-it", id, "sh", "-c",
                "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi" };

    /// <summary>
    /// Opens an interactive shell in the container in its own console window.
    /// UseShellExecute makes Windows allocate a real console for the CLI, which
    /// is what gives `docker exec -it` a working TTY; the window's lifetime
    /// belongs to the user, not the app.
    /// </summary>
    public Process StartShellProcess(string id, string? serverOs)
    {
        var (fileName, args) = Engine.BuildCommand(BuildShellArgs(id, serverOs));
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);
        return Process.Start(startInfo)
            ?? throw new DockerCliException("Could not open a terminal window for the container shell.");
    }

    private async Task<ProcessResult> RunAsync(IReadOnlyList<string> dockerArgs, TimeSpan timeout, CancellationToken ct)
    {
        var (fileName, args) = Engine.BuildCommand(dockerArgs);
        var result = await ProcessRunner.RunAsync(fileName, args, timeout, ct);
        if (result.ExitCode != 0)
            throw DockerCliException.FromResult(result);
        return result;
    }
}
