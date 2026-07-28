# Single-Instance Launch — Design

**Date:** 2026-07-28
**Status:** Approved by user in conversation.

## Problem

Launching the exe while the app is already running (window open, minimised, or hidden
in the tray) starts a second independent instance: two tray icons, duplicate
notifications, and no hint that the first instance was already there. A second launch
should instead surface the existing instance. The user confirmed strict single-instance
behavior — no gesture to deliberately run two.

## Approach

### Considered

1. **Named `Mutex` for detection + named `EventWaitHandle` as a "show yourself" signal
   (chosen).** Session-local kernel objects (`Local\` scope, per user session). The
   first instance acquires the mutex for its lifetime and waits on the event from a
   background thread; a second launch fails to acquire the mutex, sets the event, and
   shuts down before any window or tray icon is created (no flicker, no duplicate
   icon). The first instance's wait dispatches to the UI thread and reuses the
   existing restore-from-tray logic, which already handles visible, minimised, and
   hidden states. The restored window is also activated when it was already visible.
2. **`Process.GetProcessesByName`.** Racy (two simultaneous launches both pass the
   check), and activating another process's hidden window from outside runs into
   `SetForegroundWindow` foreground-rights restrictions. The event-based signal has
   the *target* process bring itself forward, which is the sanctioned path.
3. **Named pipe / remoting between instances.** Justified only when the second
   instance must pass data (file args, URLs) to the first. This app has a one-bit
   message; a named event is that bit.

## Components

- **`App.xaml.cs`**: `OnStartup` acquires `Local\DockerDualControl.SingleInstance`
  (mutex) and creates `Local\DockerDualControl.ShowExisting` (auto-reset event).
  - Not first: set the event, `Shutdown()`, return (WPF then skips the `StartupUri`
    window).
  - First: hold the mutex in a field for app lifetime; start a background
    wait loop on the event that dispatches `MainWindow.RestoreFromTray()`; release
    both handles on exit.
- **`MainWindow.RestoreFromTray`**: becomes `internal` so `App` can call it (currently
  private, used by the tray icon's Open action).

## Error handling

- Kernel-object creation failure (unexpected ACLs, name squatting) → log to the
  existing error log and continue launching normally: a stray second instance beats a
  refusal to start.
- The background wait thread is marked `IsBackground` so it never blocks process exit.

## Testing

Named kernel objects and window activation are OS integration, out of unit-test reach
by repo convention (tests cover Core logic only). Scripted verification: launch the
exe, launch it again, assert the second process exits promptly and the first
instance's window is visible afterwards — including from the hidden-in-tray state.

## Out of scope (YAGNI)

Passing command-line args to the first instance, a --new-instance escape hatch,
cross-session (Global\) enforcement.
