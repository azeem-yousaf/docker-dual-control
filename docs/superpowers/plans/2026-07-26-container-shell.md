# Container Shell (Exec Terminal) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-container "Terminal" action that opens an interactive shell inside a running container on any engine (Windows or WSL), via `docker exec -it` in a native console window.

**Architecture:** Arg construction is a pure static on `DockerService` (unit-tested); process launch mirrors the existing `StartLogsProcess` pattern but with `UseShellExecute = true` so Windows allocates a real console window. The UI adds one RelayCommand on the container row and one button in the Actions column, visible only for running containers.

**Tech Stack:** .NET 10, WPF + WPF-UI 3.0.5, CommunityToolkit.Mvvm 8.3.2, xUnit.

**Spec:** `docs/superpowers/specs/2026-07-26-container-shell-design.md`

## Global Constraints

- Tests must not require Docker or WSL (pure arg-construction tests only).
- All error surfaces go through the existing error bar (`ContainersViewModel.ReportError`); never a silent failure.
- Tests reach Core through public API only (no `InternalsVisibleTo` in this repo).
- Finish with `dotnet build DockerDualControl.slnx -c Release` (user convention).

---

### Task 1: Core — shell args + console launch

**Files:**
- Modify: `src/DockerDualControl.Core/DockerService.cs` (add two members after `StartLogsProcess`)
- Test: `tests/DockerDualControl.Tests/ShellArgsTests.cs` (new)

**Interfaces:**
- Consumes: `DockerEngine.BuildCommand(IEnumerable<string>)` (existing).
- Produces: `public static IReadOnlyList<string> DockerService.BuildShellArgs(string id, string? serverOs)` and `public Process DockerService.StartShellProcess(string id, string? serverOs)` — Task 2 calls `StartShellProcess`.

- [ ] **Step 1: Write the failing tests**

Create `tests/DockerDualControl.Tests/ShellArgsTests.cs`:

```csharp
using DockerDualControl.Core;

namespace DockerDualControl.Tests;

public class ShellArgsTests
{
    private const string LinuxShellFallback =
        "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi";

    [Fact]
    public void BuildShellArgs_LinuxEngine_PrefersBashFallsBackToSh()
    {
        var args = DockerService.BuildShellArgs("abc123", "linux");

        Assert.Equal(new[] { "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback }, args);
    }

    [Fact]
    public void BuildShellArgs_UnknownOs_TreatedAsLinux()
    {
        var args = DockerService.BuildShellArgs("abc123", null);

        Assert.Equal(new[] { "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback }, args);
    }

    [Fact]
    public void BuildShellArgs_WindowsContainers_UseCmd()
    {
        var args = DockerService.BuildShellArgs("abc123", "windows");

        Assert.Equal(new[] { "exec", "-it", "abc123", "cmd" }, args);
    }

    [Fact]
    public void ShellArgs_ComposeThroughWslEngine_Verbatim()
    {
        // The sh -c script contains ; and > — `wsl -e` must carry it as one arg.
        var engine = DockerEngine.ForWslDistro("Ubuntu-24.04");

        var (fileName, args) = engine.BuildCommand(DockerService.BuildShellArgs("abc123", "linux"));

        Assert.Equal("wsl.exe", fileName);
        Assert.Equal(new[]
        {
            "-d", "Ubuntu-24.04", "-e", "docker",
            "exec", "-it", "abc123", "sh", "-c", LinuxShellFallback,
        }, args);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/DockerDualControl.Tests --filter "FullyQualifiedName~ShellArgsTests"`
Expected: compile error — `DockerService` has no `BuildShellArgs`.

- [ ] **Step 3: Implement in DockerService**

Add to `src/DockerDualControl.Core/DockerService.cs`, after `StartLogsProcess`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DockerDualControl.Tests`
Expected: all tests PASS (new 4 + existing).

- [ ] **Step 5: Commit**

```bash
git add src/DockerDualControl.Core/DockerService.cs tests/DockerDualControl.Tests/ShellArgsTests.cs
git commit -m "feat: interactive container shell via docker exec in a console window"
```

### Task 2: App — Terminal row action

**Files:**
- Modify: `src/DockerDualControl.App/ViewModels/EngineItemViewModel.cs` (no change needed — `ServerOs` already exposed; listed for context only)
- Modify: `src/DockerDualControl.App/ViewModels/ContainerRowViewModel.cs` (add command after `OpenLogs`)
- Modify: `src/DockerDualControl.App/Views/ContainersView.xaml` (add button in Actions column between Logs and Delete)

**Interfaces:**
- Consumes: `DockerService.StartShellProcess(string id, string? serverOs)` from Task 1; `_engine.ServerOs` (existing observable property on `EngineItemViewModel`).
- Produces: `OpenShellCommand` (generated by `[RelayCommand]` from `OpenShell()`), bound in XAML.

- [ ] **Step 1: Add the command to ContainerRowViewModel**

After the `OpenLogs` command in `src/DockerDualControl.App/ViewModels/ContainerRowViewModel.cs`:

```csharp
[RelayCommand]
private void OpenShell()
{
    try
    {
        _engine.Service.StartShellProcess(Id, _engine.ServerOs);
    }
    catch (Exception ex) when (ex is DockerCliException or System.ComponentModel.Win32Exception)
    {
        _parent.ReportError($"{_engine.DisplayName}: {ex.Message}");
    }
}
```

(`Win32Exception` covers "docker/wsl not found on PATH"; `DockerCliException` the null-process guard.)

- [ ] **Step 2: Add the button to ContainersView.xaml**

In the Actions column `StackPanel`, between the Logs and Delete buttons; widen the Actions column from `Width="210"` to `Width="250"` to fit the sixth button:

```xml
<ui:Button Appearance="Transparent" Padding="6" ToolTip="Terminal" AutomationProperties.Name="Terminal"
           Icon="{ui:SymbolIcon WindowConsole20}"
           Command="{Binding OpenShellCommand}"
           Visibility="{Binding IsRunning, Converter={StaticResource BoolToVis}}" />
```

(`WindowConsole20` exists in WPF-UI 3.0.5's `SymbolRegular` enum; if the build rejects it, use `Code24`.)

- [ ] **Step 3: Build and run tests**

Run: `dotnet build DockerDualControl.slnx -c Release && dotnet test tests/DockerDualControl.Tests`
Expected: build succeeds (XAML compiles), all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/DockerDualControl.App/ViewModels/ContainerRowViewModel.cs src/DockerDualControl.App/Views/ContainersView.xaml
git commit -m "feat: Terminal action on running container rows"
```

### Task 3: Docs + final verification

**Files:**
- Modify: `README.md` (feature list)
- Modify: `CLAUDE.md` (out-of-scope note no longer lists exec)

**Interfaces:**
- Consumes: nothing new. Produces: nothing — documentation only.

- [ ] **Step 1: Update README feature list**

In the "What it does" list, after the "Live logs" bullet, add:

```markdown
- **Shell into containers.** A Terminal button on every running container opens an
  interactive shell (`docker exec -it`) in a console window — bash/sh in Linux
  containers, cmd in Windows containers — on either engine.
```

- [ ] **Step 2: Update CLAUDE.md design-docs note**

In the final "Design docs" paragraph, change the out-of-scope list `(compose, networks, exec, stats)` to `(compose, networks, stats)` since exec is now implemented.

- [ ] **Step 3: Full verification**

Run: `dotnet test tests/DockerDualControl.Tests && dotnet build DockerDualControl.slnx -c Release`
Expected: all tests PASS, build 0 warnings 0 errors.

- [ ] **Step 4: Commit**

```bash
git add README.md CLAUDE.md docs/superpowers/plans/2026-07-26-container-shell.md
git commit -m "docs: document container shell feature"
```
