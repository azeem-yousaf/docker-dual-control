# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Windows-only WPF desktop app that manages Docker containers on the Windows engine **and** inside WSL distros simultaneously, in one unified list. Requires .NET 10 SDK to build.

## Commands

```powershell
# Build (always finish a piece of work with a Release build)
dotnet build DockerDualControl.slnx -c Release

# Run the app
.\src\DockerDualControl.App\bin\Release\net10.0-windows\DockerDualControl.App.exe

# Run all tests
dotnet test tests/DockerDualControl.Tests

# Run a single test class or method
dotnet test tests/DockerDualControl.Tests --filter "FullyQualifiedName~RunSpecTests"
dotnet test tests/DockerDualControl.Tests --filter "FullyQualifiedName~RunSpecTests.MethodName"
```

Tests are pure unit tests (arg building, JSON parsing) — they do not need Docker or WSL to run.

## Architecture

Two projects plus tests, wired in `DockerDualControl.slnx`:

- `src/DockerDualControl.Core/` (net10.0) — engine discovery, CLI transport, docker JSON models. No WPF dependency; all testable logic lives here.
- `src/DockerDualControl.App/` (net10.0-windows) — WPF UI using WPF-UI (Fluent) and CommunityToolkit.Mvvm. ViewModels in `ViewModels/`, pages/dialogs in `Views/`.
- `tests/DockerDualControl.Tests/` — xUnit, references Core only.

### Core design: the docker CLI is the transport

There is no Docker API client. Every operation shells out to the docker CLI, which is what makes one code path work for both engine kinds:

| Engine | Invocation |
| --- | --- |
| Windows | `docker -H npipe:////./pipe/docker_engine <args>` |
| WSL distro | `wsl.exe -d <distro> -e docker <args>` (`-e` bypasses the distro shell so args pass verbatim) |

The pieces, all in Core:

- **`DockerEngine`** (record) — one engine's identity + `BuildCommand()`, the single place that maps docker args to a (filename, args) invocation for that engine kind.
- **`EngineDiscovery`** — probes the Windows pipe engine plus every distro from `wsl -l -q` (filtering out `docker-desktop*` utility distros) concurrently. Unreachable engines are still returned (`DiscoveredEngine.IsAvailable`/`IsInstalled`) so the UI shows them greyed out rather than hiding them.
- **`DockerService`** — typed operations over one engine (list/start/stop/restart/remove/pull/run/logs). Structured data comes from `--format "{{json .}}"`, parsed one JSON object per line by `DockerJson`. Non-zero exit → `DockerCliException` carrying stderr. Timeouts are tiered by operation class (control 5s, list 15s, long ops 120s, pull 15min; logs are unbounded/streamed via a caller-owned `Process`).
- **`EngineControl`** — detects installed-but-not-running engines and starts them: `docker desktop start` first, then the `docker` Windows service (via an elevated PowerShell, UAC prompt), then launching Docker Desktop.exe; `systemctl`/`service` inside WSL. Also switches Docker Desktop between Linux/Windows container modes (`docker desktop engine use`), pre-starting the `com.docker.service` privileged helper.
- **`RunSpec`** — builds `docker run -d …` args from the Run dialog fields; `SplitCommand` handles quoted command overrides.

### App layer conventions

- **Unified across engines — no engine switcher.** This is the app's core principle: showing one engine at a time reproduces the Docker Desktop limitation the app exists to fix. Container/image rows from all reachable engines merge into one list, each row tagged with its engine (blue = Windows, orange = WSL).
- `MainViewModel` owns the engine list and the `Containers`/`Images` child ViewModels. A 3-second auto-refresh tick reloads containers on **every** tick (plus the Images tab when active) and re-pings every engine so status chips track engines coming online/offline. Containers refresh unconditionally because `ContainerStateTracker` (Core) diffs successive snapshots into start/stop events for tray notifications — even while the window is hidden. Start-engine (180s) and mode-switch (90s) commands poll `PingAsync` because issuing the start doesn't mean the daemon is ready — engine start is fire-and-forget (`docker desktop start` blocks until fully up, so it is never awaited to completion), and the elevated `Start-Service docker` fallback is skipped when that service is Docker Desktop's own bundled daemon (it listens on `docker_engine_windows`, not the pipe the app targets).
- **Single instance**: `App.OnStartup` holds a session-local named mutex; a second launch sets a named event (waking the first instance's background wait, which restores the window) and shuts down before any window/tray icon exists. Plumbing failures fall back to a normal launch.
- **Close hides to the tray; Exit lives in the tray menu.** `TrayIcon` (WinForms `NotifyIcon`; the App project sets `UseWindowsForms` for this alone, with WinForms implicit usings removed to avoid `Brush` ambiguity) shows balloon notifications for container start/stop, only when the window is minimised or hidden. Tray setup failure degrades gracefully: close exits as before.
- **Per-engine failures are partial**: if one engine errors, the others still list; the error names the failing engine. Failed docker commands surface stderr in an in-app infobar — never fail silently.
- Unhandled exceptions are logged to `%TEMP%\DockerDualControl.error.log` (see `App.xaml.cs`).

## Design docs

`docs/superpowers/specs/` and `docs/superpowers/plans/` hold the original design and implementation plan, including rationale for rejected approaches (Docker.DotNet, engine-switching UI) and the out-of-scope list (compose, networks, stats).
