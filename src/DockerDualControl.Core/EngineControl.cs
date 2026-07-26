using System.Diagnostics;

namespace DockerDualControl.Core;

/// <summary>Detects whether an engine is installed on the system and can start it.</summary>
public static class EngineControl
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    /// <summary>True when the engine is installed on the system, even if it is not running.</summary>
    public static async Task<bool> EngineExistsAsync(DockerEngine engine, CancellationToken ct = default)
    {
        try
        {
            if (engine.Kind == EngineKind.Windows)
                return await WindowsDockerServiceExistsAsync(ct) || DockerDesktopExePath() is not null;

            var result = await ProcessRunner.RunAsync("wsl.exe",
                new[] { "-d", engine.WslDistro!, "-u", "root", "sh", "-c", "command -v dockerd" },
                ProbeTimeout, ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Issues the platform-appropriate start command. Returns once the command is issued;
    /// the engine may take a while to come online (poll <see cref="DockerService.PingAsync"/>).
    /// </summary>
    public static async Task StartEngineAsync(DockerEngine engine, CancellationToken ct = default)
    {
        if (engine.Kind == EngineKind.Windows)
            await StartWindowsEngineAsync(ct);
        else
            await StartWslEngineAsync(engine.WslDistro!, ct);
    }

    private static async Task<bool> WindowsDockerServiceExistsAsync(CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("sc.exe", new[] { "query", "docker" }, ProbeTimeout, ct);
        return result.ExitCode == 0;
    }

    private static string? DockerDesktopExePath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Docker", "Docker", "Docker Desktop.exe");
        return File.Exists(path) ? path : null;
    }

    /// <summary>True when WSL is installed, which Docker Desktop's Linux mode requires.</summary>
    public static async Task<bool> IsWslInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await ProcessRunner.RunAsync("wsl.exe", new[] { "--status" }, ProbeTimeout, ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>Switches Docker Desktop between Windows and Linux containers.</summary>
    public static async Task SwitchEngineModeAsync(string mode, CancellationToken ct = default)
    {
        // Windows containers need Docker Desktop's privileged helper; if it is stopped,
        // Docker blocks the switch behind its own elevation dialog. Starting it up front
        // turns that into a single regular UAC prompt.
        if (mode == "windows")
            await EnsurePrivilegedHelperRunningAsync(ct);

        var result = await ProcessRunner.RunAsync(
            "docker", new[] { "desktop", "engine", "use", mode }, TimeSpan.FromSeconds(120), ct);
        if (result.ExitCode != 0)
            throw DockerCliException.FromResult(result);
    }

    private static async Task EnsurePrivilegedHelperRunningAsync(CancellationToken ct)
    {
        var query = await ProcessRunner.RunAsync(
            "sc.exe", new[] { "query", "com.docker.service" }, ProbeTimeout, ct);
        if (query.ExitCode != 0)
            return; // helper not installed (not a Docker Desktop setup); nothing to pre-start
        if (query.StdOut.Contains("RUNNING"))
            return;
        await StartServiceElevatedAsync("com.docker.service", ct);
    }

    private static async Task StartWindowsEngineAsync(CancellationToken ct)
    {
        // Docker Desktop needs its privileged helper; pre-start it so Desktop does not
        // block behind its own elevation dialog.
        await EnsurePrivilegedHelperRunningAsync(ct);

        // Prefer the Docker Desktop CLI: it brings the engine up without opening the dashboard.
        if (await TryDockerDesktopCliStartAsync(ct))
            return;

        if (await WindowsDockerServiceExistsAsync(ct))
        {
            await StartServiceElevatedAsync("docker", ct);
            return;
        }

        var desktopExe = DockerDesktopExePath()
            ?? throw new DockerCliException(
                "No Docker engine found on Windows: neither the 'docker' service nor Docker Desktop is installed.");
        using var _ = Process.Start(new ProcessStartInfo { FileName = desktopExe, UseShellExecute = true });
    }

    private static async Task<bool> TryDockerDesktopCliStartAsync(CancellationToken ct)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(
                "docker", new[] { "desktop", "start" }, TimeSpan.FromSeconds(120), ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception)
        {
            return false; // CLI missing or unresponsive; fall through to the other strategies
        }
    }

    /// <summary>Starting a Windows service needs admin rights, so this triggers a UAC prompt.</summary>
    private static async Task StartServiceElevatedAsync(string serviceName, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command Start-Service -Name {serviceName}",
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new DockerCliException($"Could not launch an elevated shell to start the {serviceName} service.");
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0)
                throw new DockerCliException($"Starting the {serviceName} service failed (exit code {process.ExitCode}).");
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new DockerCliException($"Administrator approval is required to start the {serviceName} service.");
        }
    }

    private static async Task StartWslEngineAsync(string distro, CancellationToken ct)
    {
        var result = await ProcessRunner.RunAsync("wsl.exe",
            new[]
            {
                "-d", distro, "-u", "root", "sh", "-c",
                "if [ -d /run/systemd/system ]; then systemctl start docker; else service docker start; fi",
            },
            StartTimeout, ct);
        if (result.ExitCode != 0)
            throw DockerCliException.FromResult(result);
    }
}
