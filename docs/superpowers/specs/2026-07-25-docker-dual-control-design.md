# Docker Dual Control — Design

**Date:** 2026-07-25
**Status:** Approved via /goal autonomous mode (user not available for interactive review; decisions documented here).

## Problem

Docker Desktop (in Windows-containers mode) only shows containers running on the
Windows Docker engine. Containers running on a Docker engine *inside* a WSL distro
(e.g. docker-ce in Ubuntu-24.04) are invisible to it. The user wants a single,
friendly GUI that manages containers on **both** engines: list, start, stop,
restart, remove, create/run containers, view logs, and manage images.

## Environment (verified on this machine)

- Windows Docker engine v29.6.2 reachable at `\\.\pipe\docker_engine` (OS = windows).
- WSL2 distro `Ubuntu-24.04` with its own docker engine v29.6.2 (reached via `wsl -d Ubuntu-24.04 docker …`).
- .NET SDKs 8/9/10 installed. App targets **.NET 8 (LTS)**.

## Approach

### Considered

1. **Docker.DotNet API client per engine.** Clean typed API, but the WSL engine's
   unix socket is not reachable from Windows without exposing TCP on the daemon or
   proxying streams through `wsl.exe` — both fragile or invasive.
2. **Docker CLI as transport (chosen).** Every operation shells out to the docker CLI:
   - Windows engine: `docker -H npipe:////./pipe/docker_engine <args>`
   - WSL engine: `wsl.exe -d <distro> docker <args>`
   Structured data via `--format "{{json .}}"`. No daemon reconfiguration, works with
   any distro that has a docker CLI + daemon, trivially extensible to more distros.
3. **Hybrid (API for Windows, CLI for WSL).** Two code paths for no user-visible gain.

CLI latency (tens of ms per call) is irrelevant for a GUI refreshing every few seconds.

### UI framework

**WPF on .NET 8 + WPF-UI (`WPF-UI` NuGet, Fluent design)**: modern Windows 11 look,
NavigationView sidebar, dark/light theme, mature data binding. (Avalonia/WinUI3
rejected: more setup friction, no benefit for a Windows-only tool.)

## Architecture

```
DockerDualControl/            WPF app (net8.0-windows)
  Core/
    ProcessRunner.cs          async process exec -> (exitCode, stdout, stderr)
    DockerEngine.cs           one engine = executable + arg prefix + display name
    EngineDiscovery.cs        finds Windows pipe engine + WSL distro engines
    DockerService.cs          typed operations over an engine (ps, start, stop, ...)
    Models/                   ContainerInfo, ImageInfo, PortMapping ... (JSON parse)
  ViewModels/                 MVVM (CommunityToolkit.Mvvm)
  Views/                      Pages: Containers, Images; dialogs: RunContainer, Logs
DockerDualControl.Tests/      xUnit: arg building + JSON parsing
```

- **`DockerEngine`** (record): `Id`, `DisplayName`, `Kind` (Windows | Wsl), and how to
  invoke docker (`FileName`, `ArgumentPrefix`). All commands flow through one method:
  `RunAsync(params string[] dockerArgs)`.
- **`EngineDiscovery`**: probes `npipe` engine with `docker version`; runs `wsl -l -q`,
  filters out `docker-desktop*`, probes each distro with `docker version` (short timeout).
  Non-responsive engines are still listed (grayed out) with a "not available" state.
- **`DockerService`**: `ListContainersAsync`, `ListImagesAsync`, `StartAsync`, `StopAsync`,
  `RestartAsync`, `RemoveAsync`, `RunContainerAsync(RunSpec)`, `PullAsync`, `GetLogs`
  (streaming `docker logs -f` for the log viewer).

## UI design

**Unified across engines — no engine switching.** (Revised during implementation at the
user's request: the original design had an engine selector that showed one engine at a
time. Seeing one engine at a time reproduces the exact Docker Desktop limitation this
app exists to fix.)

- **Top strip:** one status chip per discovered engine, always visible — accent dot
  (blue = Windows, orange = WSL), name, availability dot + version. Refresh button and
  auto-refresh toggle (default on, 3 s). Theme follows system.
- **Left sidebar:** Containers, Images.
- **Containers page:** rows merged from *all* reachable engines, sorted by engine then
  name, each row carrying an engine chip. Search box (matches name, image, or engine),
  "Run a container" button; columns: status dot, engine, name + short id, image, status,
  ports; per-row actions start/stop/restart/logs/delete (delete confirms first).
- **Images page:** merged the same way with an engine column. Pull bar = image reference
  + target-engine picker + Pull button with progress. Row actions: run (opens Run dialog
  prefilled with that image and engine), delete (confirm).
- **Run container dialog:** engine picker, image (required), name, port mappings
  (host:container, one per line), env vars (KEY=value), volumes (host:container),
  command override. Maps to `docker run -d …` against the chosen engine.
- **Per-engine failures are partial:** if one engine errors, the others still list; the
  error bar names the engine that failed.
- **Logs viewer:** modal-less window, monospace, follows output, stop-follow toggle.
- **Errors:** every failed docker command surfaces stderr in an in-app snackbar/infobar,
  never a silent failure. Engine unreachable → page-level empty state with retry.

## Error handling

- All CLI calls have timeouts (5 s control commands, 30 s pull/run; logs unbounded, streamed).
- stderr + non-zero exit → `DockerCliException` carrying stderr; ViewModels catch and
  show the message.
- WSL distro asleep: first call auto-starts it (that's inherent to `wsl -d`); discovery
  tolerates slow first response.

## Testing

- xUnit project covering: engine argument construction (Windows vs WSL prefixing),
  `docker ps`/`images` JSON line parsing (real captured samples), RunSpec → `docker run`
  argument generation (quoting, ports, env, volumes).
- GUI verified by building and launching the app against the real engines on this machine.

## Out of scope (YAGNI)

Compose, volumes/networks management pages, exec-into-container terminal, container
stats graphs, Kubernetes, multiple simultaneous engine views. Architecture (one
`DockerService` per engine) leaves room for these later.
