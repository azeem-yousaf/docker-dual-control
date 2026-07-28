# Close to Tray + Container Start/Stop Notifications — Design

**Date:** 2026-07-28
**Status:** Approved via goal autonomous mode (user not available for interactive review; decisions documented here).

## Problem

Closing the window exits the app, so nothing watches the engines while it is not on
screen. The user wants the app to keep running in the system tray when closed, and —
while minimised or in the tray — to notify them when a container starts or stops on
any engine.

## Approach

### Tray + notifications: considered

1. **WinForms `NotifyIcon` (chosen).** `<UseWindowsForms>true</UseWindowsForms>` in the
   App project; zero new NuGet packages. One API covers both needs: the tray icon with
   a context menu, and `ShowBalloonTip`, which Windows 10/11 renders as a native toast
   notification. Mature and boring — exactly right for a background affordance.
2. **WPF-UI tray package.** Keeps the UI library consistent, but tray support lives in
   a separate package and has no notification support, so a second mechanism would
   still be needed.
3. **`Microsoft.Toolkit.Uwp.Notifications` toasts.** Richer toasts, but adds a package
   plus AUMID/COM-activation plumbing for an unpackaged app. YAGNI for one-line
   start/stop messages.

### Change detection: considered

1. **Diff successive `docker ps` snapshots (chosen).** The app already lists containers
   every 3 s. A pure Core class diffs consecutive snapshots per engine and emits
   Started/Stopped events. No new transport, works identically for both engine kinds,
   and is unit-testable without Docker.
2. **`docker events --format json` streamed per engine.** Instant and precise, but adds
   a long-lived process per engine that must be supervised (WSL restarts, engine
   offline/online), duplicating state the polling loop already tracks. Not worth it at
   a 3 s granularity.

## Behavior

- **Close (X)** hides the window to the tray instead of exiting. The first hide shows a
  one-time balloon: the app is still running and where to find it.
- **Minimize** keeps its normal taskbar behavior (notifications fire in that state too).
- **Tray icon** is always present while the app runs (standard for tray-capable apps,
  and it is the only handle on the app once hidden). Double-click or context-menu
  *Open* restores/activates the window; *Exit* really quits (disposes the icon, then
  `Application.Shutdown`). Clicking a notification balloon also restores the window.
- **Notifications** fire only when the window is minimised or hidden — foreground use
  shows the live list already. Multiple changes in one tick batch into one balloon.
  Notification text names the container, the event, and the engine, e.g.
  `web-1 started (Windows)` / `db stopped (WSL: Ubuntu)`.

## Components

- **`ContainerStateTracker`** (Core, pure): `Update(engineId, containers)` diffs against
  the previous snapshot for that engine and returns `ContainerStateChange` records
  (`EngineId`, `ContainerId`, `ContainerName`, `Kind` Started/Stopped).
  - First snapshot per engine is baseline: no events.
  - Started = transitioned to running, or first appeared already running.
  - Stopped = transitioned from running to not running, or disappeared while running
    (`docker rm -f`).
  - Non-running lifecycle (created/exited containers appearing or disappearing) emits
    nothing.
  - Engines are independent; an engine whose listing failed is simply not updated that
    tick (per-engine failure is already partial in the refresh loop), so an offline
    engine never spams Stopped events. When it returns, the diff against its last-known
    snapshot reflects what actually changed.
- **`ContainersViewModel`** (App): after each refresh, feeds each engine's *successful*
  result into the tracker and raises a `StateChangesDetected` event with the batch.
- **`MainViewModel.AutoRefreshTickAsync`** (App): now always refreshes Containers
  (silently) each tick, plus Images when that tab is active. Previously only the active
  tab refreshed, which would blind the tracker while on the Images tab or in the tray.
- **`TrayIcon`** (App, new class owning the `NotifyIcon`): created by `MainWindow` on
  load using the executable's associated icon; exposes Open/Exit intents and
  `ShowContainerChanges(changes)`; disposed on real exit. `MainWindow` wires
  close-to-hide, restore, and the minimised-or-hidden notification gate.

## Error handling

- Tray icon creation failure (no shell, missing icon) must not break the main window:
  caught and logged to the existing `%TEMP%\DockerDualControl.error.log`; the app then
  behaves as before (close exits).
- The tracker never throws on odd input (duplicate IDs, empty names — last write wins,
  fall back to short ID for display).
- Balloon text is capacity-limited by Windows; batches are summarised (first few
  changes + "and N more") rather than truncated mid-word.

## Testing

- xUnit on `ContainerStateTracker`: baseline emits nothing; stop, start, restart
  transitions; appear-running and disappear-while-running; non-running lifecycle
  silence; per-engine independence (skipped engine emits nothing, later diff correct).
- Tray behavior (hide on close, balloon display) is WPF/shell integration — manual
  verification, consistent with prior features.

## Out of scope (YAGNI)

Settings UI to toggle the behavior, start-minimised/auto-start with Windows, rich
toast actions, per-container notification filters, image/pull notifications.
