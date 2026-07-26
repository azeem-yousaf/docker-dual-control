# Docker Dual Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A WPF desktop app that manages containers and images on both the Windows Docker engine and Docker engines inside WSL distros.

**Architecture:** All docker operations shell out to the docker CLI — `docker -H npipe:////./pipe/docker_engine …` for the Windows engine, `wsl.exe -d <distro> docker …` for WSL engines — parsed from `--format "{{json .}}"` output. MVVM WPF UI (WPF-UI Fluent library) with an engine selector, Containers page, Images page, Run-container dialog, and streaming logs viewer.

**Tech Stack:** .NET 8, WPF, WPF-UI (lepo.co) 3.x, CommunityToolkit.Mvvm, System.Text.Json, xUnit.

## Global Constraints

- Target framework: `net8.0-windows` (app), `net8.0` (core lib + tests).
- Solution name/dir: `DockerDualControl` in repo root.
- Windows engine host string: `npipe:////./pipe/docker_engine` (exact).
- WSL invocation: `wsl.exe -d <distro> docker <args…>`; skip distros named `docker-desktop` / `docker-desktop-data`.
- Every failed CLI call surfaces stderr to the user; no silent failures.
- Control-command timeout 5 s; pull/run 120 s; log streaming unbounded.

---

### Task 1: Solution scaffold + Core library + ProcessRunner

**Files:**
- Create: `DockerDualControl.sln`, `src/DockerDualControl.Core/DockerDualControl.Core.csproj`, `src/DockerDualControl.Core/ProcessRunner.cs`, `tests/DockerDualControl.Tests/DockerDualControl.Tests.csproj`, `.gitignore`

**Interfaces:**
- Produces: `record ProcessResult(int ExitCode, string StdOut, string StdErr)`; `static Task<ProcessResult> ProcessRunner.RunAsync(string fileName, IReadOnlyList<string> args, TimeSpan timeout, CancellationToken ct = default)` — never throws on non-zero exit; throws `TimeoutException` on timeout. Uses `ProcessStartInfo.ArgumentList` (no manual quoting), `CreateNoWindow = true`.

Steps: scaffold projects, write test that `ProcessRunner.RunAsync("cmd", ["/c","echo hi"])` returns exit 0 / "hi", run (fail), implement, run (pass), commit.

### Task 2: DockerEngine model + argument construction

**Files:**
- Create: `src/DockerDualControl.Core/DockerEngine.cs`
- Test: `tests/DockerDualControl.Tests/DockerEngineTests.cs`

**Interfaces:**
- Produces: `enum EngineKind { Windows, Wsl }`; `sealed record DockerEngine(string Id, string DisplayName, EngineKind Kind, string? WslDistro)` with `(string FileName, List<string> Args) BuildCommand(IEnumerable<string> dockerArgs)`:
  - Windows → `("docker", ["-H","npipe:////./pipe/docker_engine", …dockerArgs])`
  - Wsl → `("wsl.exe", ["-d", WslDistro, "docker", …dockerArgs])`
- Static factories `DockerEngine.Windows()`, `DockerEngine.ForWslDistro(string distro)`.

TDD: tests assert exact arg lists for both kinds; commit.

### Task 3: Models + JSON line parsing

**Files:**
- Create: `src/DockerDualControl.Core/Models/ContainerInfo.cs`, `Models/ImageInfo.cs`, `src/DockerDualControl.Core/DockerJson.cs`
- Test: `tests/DockerDualControl.Tests/DockerJsonTests.cs`

**Interfaces:**
- Produces: `sealed record ContainerInfo(string Id, string Names, string Image, string State, string Status, string Ports, string CreatedAt)` with computed `bool IsRunning => State == "running"`; `sealed record ImageInfo(string Id, string Repository, string Tag, string Size, string CreatedSince)`;
  `static List<T> DockerJson.ParseLines<T>(string stdout)` — one JSON object per line (docker `--format "{{json .}}"`), skips blank lines, tolerates unknown fields (case-insensitive mapping of docker's PascalCase keys: `ID`, `Names`, `Image`, `State`, `Status`, `Ports`, `CreatedAt`, `Repository`, `Tag`, `Size`, `CreatedSince`).
- Note: `ID` maps via `[JsonPropertyName("ID")]`.

TDD with real captured sample lines from `docker ps -a --format "{{json .}}"`; commit.

### Task 4: DockerService (typed operations)

**Files:**
- Create: `src/DockerDualControl.Core/DockerService.cs`, `src/DockerDualControl.Core/RunSpec.cs`, `src/DockerDualControl.Core/DockerCliException.cs`
- Test: `tests/DockerDualControl.Tests/RunSpecTests.cs`

**Interfaces:**
- Produces:
  - `sealed class DockerCliException(string message) : Exception` (message = stderr trimmed, fallback "docker exited with code N").
  - `sealed record PortMapping(string Host, string Container)`, `sealed record EnvVar(string Key, string Value)`, `sealed record VolumeMapping(string Host, string Container)`;
  - `sealed class RunSpec { string Image; string? Name; List<PortMapping> Ports; List<EnvVar> Env; List<VolumeMapping> Volumes; string? Command; }` with `List<string> ToDockerArgs()` → `["run","-d", "--name",name?, "-p","h:c"…, "-e","K=V"…, "-v","h:c"…, image, …command.Split(' ')?]`.
  - `sealed class DockerService(DockerEngine engine)`: `Task<List<ContainerInfo>> ListContainersAsync(CancellationToken)` (`ps -a --no-trunc --format "{{json .}}"`), `ListImagesAsync` (`images --format "{{json .}}"`), `StartContainerAsync(id)`, `StopContainerAsync(id)`, `RestartContainerAsync(id)`, `RemoveContainerAsync(id, force: true)`, `RemoveImageAsync(id)`, `PullImageAsync(reference)`, `RunContainerAsync(RunSpec)`, `Task<bool> PingAsync()` (`version --format "{{.Server.Version}}"`, 10 s timeout, false on failure), `Process StartLogsProcess(string id)` (returns started `docker logs -f --tail 500` process for streaming).
  - All non-ping ops throw `DockerCliException` on non-zero exit.

TDD on `RunSpec.ToDockerArgs()`; commit.

### Task 5: EngineDiscovery

**Files:**
- Create: `src/DockerDualControl.Core/EngineDiscovery.cs`
- Test: `tests/DockerDualControl.Tests/EngineDiscoveryTests.cs` (distro-name filtering only; live probing is integration-verified manually)

**Interfaces:**
- Produces: `sealed record DiscoveredEngine(DockerEngine Engine, bool IsAvailable, string? Version)`; `static Task<List<DiscoveredEngine>> EngineDiscovery.DiscoverAsync(CancellationToken)`:
  1. Windows engine: always listed; availability = `DockerService.PingAsync()`.
  2. `wsl.exe -l -q` → distro names (**strip null chars** — wsl.exe emits UTF-16 output; decode by removing `\0` and splitting lines), filter empties + `docker-desktop*` via exposed `static IEnumerable<string> FilterDistros(IEnumerable<string> raw)`.
  3. Each remaining distro probed in parallel with ping.

TDD on `FilterDistros` incl. null-char stripping; commit.

### Task 6: WPF app scaffold + main window shell

**Files:**
- Create: `src/DockerDualControl.App/DockerDualControl.App.csproj` (net8.0-windows, `UseWPF`, refs Core, WPF-UI, CommunityToolkit.Mvvm), `App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `ViewModels/MainViewModel.cs`, `Assets/` app icon
- Modify: `DockerDualControl.sln`

**Interfaces:**
- Produces: `MainViewModel`: `ObservableCollection<DiscoveredEngine> Engines`, `DiscoveredEngine? SelectedEngine` (change → reload current page), `bool AutoRefresh` (3 s DispatcherTimer), `string? ErrorMessage` (bound to InfoBar/Snackbar), `IRelayCommand RefreshCommand`.
- Shell: `ui:FluentWindow`, top bar = engine ComboBox (status dot + name + version), refresh + auto-refresh toggle; left `ui:NavigationView` with Containers / Images pages; Fluent theme follows system.

Verify: `dotnet build` passes, app launches showing both engines in selector. Commit.

### Task 7: Containers page

**Files:**
- Create: `Views/ContainersPage.xaml(.cs)`, `ViewModels/ContainersViewModel.cs`, `ViewModels/ContainerRowViewModel.cs`

**Interfaces:**
- Consumes: `DockerService` (Task 4), `MainViewModel.SelectedEngine`.
- Produces: search-filtered list; per-row commands Start/Stop/Restart/Logs/Remove (Remove → `ui:MessageBox` confirm); busy row state while a command runs; status dot green (running) / gray (exited); columns Name, Image, State/Status, Ports, Created. Header: search TextBox, "Run a container" button, container count.

Verify against live Windows + WSL engines; commit.

### Task 8: Run-container dialog + Images page

**Files:**
- Create: `Views/RunContainerDialog.xaml(.cs)`, `ViewModels/RunContainerViewModel.cs`, `Views/ImagesPage.xaml(.cs)`, `ViewModels/ImagesViewModel.cs`

**Interfaces:**
- Consumes: `RunSpec` (Task 4).
- Produces: dialog with Image (required), Name, dynamic Ports/Env/Volumes row editors, Command; OK → `RunContainerAsync`, error text inline. Images page: pull bar (TextBox + Pull button + progress ring), grid Repo/Tag/Size/Created with Run (prefills dialog) + Delete (confirm).

Verify live (pull `hello-world` on WSL engine, run it); commit.

### Task 9: Logs viewer

**Files:**
- Create: `Views/LogsWindow.xaml(.cs)`, `ViewModels/LogsViewModel.cs`

**Interfaces:**
- Consumes: `DockerService.StartLogsProcess(id)`.
- Produces: non-modal window per container; monospace TextBox appended from process stdout/stderr on dispatcher; auto-scroll toggle; kills process on close.

Verify live; commit.

### Task 10: Polish + end-to-end verification

- Empty states ("No containers — Run one to get started"), engine-unavailable state with Retry, window title/icon, README.md with screenshot instructions.
- Run full test suite; launch app; verify on both engines: list/start/stop/restart/remove/logs/run/pull/rmi.
- Final commit.
