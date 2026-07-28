# Close to Tray + Notifications — Implementation Plan

Spec: `../specs/2026-07-28-tray-and-notifications-design.md`

## Task 1 — `ContainerStateTracker` (Core, TDD)

1. Tests first: `tests/DockerDualControl.Tests/ContainerStateTrackerTests.cs`
   - first snapshot per engine → no events
   - running → exited → Stopped; exited → running → Started
   - appears already running → Started; new non-running container → nothing
   - disappears while running → Stopped; disappears while exited → nothing
   - engines independent: updating engine A never emits for engine B; an engine
     skipped for a tick diffs correctly on its next update
   - empty name falls back to short ID in `DisplayName`
2. Implement `src/DockerDualControl.Core/ContainerStateTracker.cs`:
   `ContainerChangeKind` enum, `ContainerStateChange` record,
   `Update(string engineId, IReadOnlyList<ContainerInfo>)` →
   `IReadOnlyList<ContainerStateChange>`; internal per-engine
   `Dictionary<string, Dictionary<string, (name, running)>>`.

## Task 2 — Wire into refresh loop (App)

1. `ContainersViewModel`: own a `ContainerStateTracker`; after `Task.WhenAll`, call
   `Update` for each engine result with no error; raise
   `event Action<IReadOnlyList<ContainerStateChange>>? StateChangesDetected` when the
   batch is non-empty.
2. `MainViewModel.AutoRefreshTickAsync`: always `Containers.RefreshAsync(clear: false,
   silent: true)`; additionally refresh Images when `SelectedTabIndex == 1`.

## Task 3 — Tray icon + close-to-tray (App)

1. `DockerDualControl.App.csproj`: add `<UseWindowsForms>true</UseWindowsForms>`.
2. New `src/DockerDualControl.App/TrayIcon.cs`: wraps `System.Windows.Forms.NotifyIcon`
   (icon from `Icon.ExtractAssociatedIcon(Environment.ProcessPath)`), context menu
   Open/Exit, double-click + balloon-click → `OpenRequested`; `ShowContainerChanges`
   batches to one balloon (cap listed lines, "… and N more"); one-time
   `ShowFirstHideHint`; `IDisposable`.
3. `MainWindow.xaml.cs`: create `TrayIcon` in `OnLoaded` (try/catch → log, degrade to
   old behavior); `Closing` → cancel + `Hide()` unless exiting; Open → show/restore/
   activate; Exit → dispose icon, `Application.Current.Shutdown()`; subscribe
   `Containers.StateChangesDetected` → notify only when
   `WindowState == Minimized || !IsVisible`.

## Task 4 — Verify + docs

1. `dotnet test tests/DockerDualControl.Tests`
2. `dotnet build DockerDualControl.slnx -c Release`
3. CLAUDE.md: note always-on containers refresh + tray/notification convention.
4. Commit.
