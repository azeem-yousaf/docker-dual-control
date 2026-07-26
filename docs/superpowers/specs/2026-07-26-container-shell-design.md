# Container Shell ("SSH into container") — Design

**Date:** 2026-07-26
**Status:** Approved via goal autonomous mode (user not available for interactive review; decisions documented here).

## Problem

The user wants to "SSH into a container": open an interactive shell inside a running
container, from the app, on any engine (Windows or WSL). Today the app can view logs
but offers no way to get a prompt inside a container.

## Approach

### Considered

1. **Native console window running `docker exec -it` (chosen).** A row action starts
   the docker CLI in its own console window. Windows allocates a real console (full
   TTY: arrows, tab-completion, colors, Ctrl-C) when a GUI app launches a console
   executable with `UseShellExecute = true`. Reuses `DockerEngine.BuildCommand`, so
   both engines work identically:
   - Windows: `docker -H npipe:////./pipe/docker_engine exec -it <id> <shell>`
   - WSL: `wsl.exe -d <distro> -e docker exec -it <id> <shell>`
2. **Embedded terminal inside the WPF window.** No mature free terminal-emulator
   control for WPF; writing one (VT parsing, input handling) is a project in itself
   for no functional gain over a native console.
3. **Real SSH (sshd in the container).** Requires installing/starting sshd in every
   container, exposing ports, managing keys — invasive and strictly worse than
   `docker exec`, which needs nothing inside the container.

### Shell selection

The container's OS decides the shell; the engine's pinged `Server.Os` is the source
of truth (containers always match their engine's OS):

- **Linux** (`sh` always present, bash preferred when available):
  `sh -c "if command -v bash >/dev/null 2>&1; then exec bash; else exec sh; fi"`
- **Windows containers** (`Server.Os == "windows"`): `cmd`

## Components

- **`DockerService.BuildShellArgs(id, serverOs)`** (Core, static, pure): returns the
  `exec -it …` docker args for the container's OS. Unit-testable.
- **`DockerService.StartShellProcess(id, serverOs)`** (Core): mirrors the existing
  `StartLogsProcess` pattern — builds the full command via `Engine.BuildCommand` and
  starts it with `UseShellExecute = true` so it gets its own console window. The
  console window is its own lifetime; the app does not track the process.
- **`ContainerRowViewModel.OpenShell`** (App): RelayCommand calling
  `StartShellProcess`; failures (e.g. docker CLI missing) surface through the existing
  error bar via `ReportError`. Exited-shell exit codes are not observed — closing the
  window is the normal end of a session.
- **`ContainersView.xaml`**: a Terminal button in the Actions column, visible only for
  running containers (same `IsRunning` visibility pattern as Stop/Restart).

## Error handling

- Launch failure (`Win32Exception`, e.g. docker not on PATH) → error bar names the
  engine, consistent with every other action.
- Container stops while the window is open → docker exec ends; the console window
  shows docker's own message. Nothing for the app to do.
- Shell exits → console window closes (docker.exe terminates). No orphan tracking
  needed; the process belongs to the user.

## Testing

- xUnit on `BuildShellArgs`: linux args (bash-fallback-to-sh via `sh -c`), windows
  args (`cmd`), null/unknown OS treated as linux.
- Composition through `BuildCommand` covered by existing engine tests; one test
  asserts the full WSL invocation for exec args.
- Launching a real console window is manual-verification territory (as with the
  logs window in the original design).

## Out of scope (YAGNI)

Shell picker UI, exec-as-user options, command history, embedded terminal, real SSH.
