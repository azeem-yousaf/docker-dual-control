# Tray Tooltip Container Summary — Design

**Date:** 2026-07-28
**Status:** Approved via goal autonomous mode (user not available for interactive review; decisions documented here).

## Problem

The tray icon's hover tooltip is the static string "Docker Dual Control". Once the
window is hidden to the tray, the only way to see how the containers are doing is to
reopen the window. The user wants hovering over the tray icon to show a summary of the
containers — e.g. *3 running, 1 stopped* — presented nicely.

## Approaches considered

1. **`NotifyIcon.Text` native tooltip (chosen).** The tray icon already exists as a
   WinForms `NotifyIcon`; its `Text` property *is* the hover tooltip, rendered natively
   by the shell. Multi-line via `\n`, zero new dependencies, zero event plumbing —
   the OS handles hover show/hide, positioning and DPI. Constraint: hard 127-character
   limit (`ArgumentOutOfRangeException` beyond it), so formatting must budget length.
2. **Custom WPF flyout on hover.** Arbitrary richness (colored chips per engine), but
   `NotifyIcon` has no hover-enter/leave events — only `MouseMove` — so showing and
   dismissing a floating window means timers, taskbar-position math, and a non-native
   feel. Heavy machinery for four numbers. Rejected.
3. **Balloon/toast.** Not hover-triggered; wrong interaction. Rejected.

"Look nice" is delivered inside option 1: a tidy multi-line layout with Unicode state
glyphs rather than one flat sentence.

## Tooltip format

```
Docker Dual Control
● 3 running · ○ 1 stopped
Windows 2/3 · Ubuntu 1/1
```

- **Line 1** — app name, always present (keeps the tooltip recognisable).
- **Line 2** — overall counts across all reachable engines. A zero side is omitted
  (`● 4 running`); zero containers total shows `No containers`; no reachable engines
  shows `No engines reachable`.
- **Line 3** — per-engine `running/total` breakdown (engine short names: `Windows`,
  distro name for WSL), joined with `·`. Only present with 2+ reachable engines —
  with one engine it would repeat line 2.

**Length budgeting:** build all lines; if the result exceeds 127 chars, drop line 3;
if a pathological case still exceeds (it cannot with current wording, but the formatter
guarantees the invariant rather than the caller), hard-truncate with `…`. The setter
never throws.

## Components

- **`TrayStatusFormatter`** (Core, new, pure): `Format(IReadOnlyList<EngineContainerSummary>)`
  → tooltip string per the rules above. `EngineContainerSummary(string EngineName,
  int Running, int Stopped)` is a Core record so the formatter is unit-testable.
- **`ContainersViewModel`** (App): already fetches per-engine listings every 3 s tick.
  After each refresh it additionally raises `SummaryUpdated` with one
  `EngineContainerSummary` per **successful** listing (errored engines are excluded —
  their counts are unknown; same partial-failure stance as the list itself). The
  no-engines early return raises with an empty list so the tooltip degrades honestly.
- **`TrayIcon`** (App): new `UpdateStatus(summaries)` sets `NotifyIcon.Text` from the
  formatter. The tooltip updates on every refresh, regardless of window visibility —
  the tray icon is always present.
- **`MainWindow`**: wires `SummaryUpdated` → `UpdateStatus` alongside the existing
  state-change wiring.

## Error handling

- Formatter output is guaranteed ≤ 127 chars (enforced inside the formatter), so
  setting `Text` cannot throw.
- A tick where every engine errors produces an empty summary list →
  `No engines reachable`; the next good tick corrects it. No stale counts are shown.

## Testing

xUnit on `TrayStatusFormatter` (pure, no Docker needed): no engines; zero containers;
mixed running/stopped; zero-side omission; single engine (no breakdown line);
multi-engine breakdown; long distro names dropping the breakdown line; the ≤ 127
invariant under absurd inputs. Hover rendering is shell integration — manual
verification, consistent with prior tray features.

## Out of scope (YAGNI)

Custom flyout UI, per-container names in the tooltip, image counts, engine
online/offline change notifications (covered by existing balloons), settings to
configure the tooltip.
