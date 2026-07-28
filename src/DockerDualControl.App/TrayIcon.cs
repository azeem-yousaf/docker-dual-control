using DockerDualControl.Core;
using Forms = System.Windows.Forms;

namespace DockerDualControl.App;

/// <summary>
/// The system-tray presence: icon, Open/Exit menu, and balloon notifications
/// (rendered by Windows 10/11 as native toasts). Owned by MainWindow; disposing
/// removes the icon from the tray.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _firstHideHintShown;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Docker Dual Control", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "Docker Dual Control",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _notifyIcon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke();
    }

    /// <summary>Shown once, the first time a close is turned into a hide, so the
    /// changed close behavior is never a surprise.</summary>
    public void ShowFirstHideHint()
    {
        if (_firstHideHintShown)
            return;
        _firstHideHintShown = true;
        _notifyIcon.ShowBalloonTip(5000, "Docker Dual Control is still running",
            "The app keeps watching your engines from the system tray. " +
            "Double-click the tray icon to open it again; use Exit to quit.",
            Forms.ToolTipIcon.Info);
    }

    public void ShowContainerChanges(IReadOnlyList<ContainerStateChange> changes)
    {
        if (changes.Count == 0)
            return;

        // Balloon text space is limited; list a few and summarize the rest.
        const int maxLines = 3;
        var lines = changes.Take(maxLines)
            .Select(c => $"{c.ContainerName} {(c.Kind == ContainerChangeKind.Started ? "started" : "stopped")} ({EngineLabel(c.EngineId)})")
            .ToList();
        if (changes.Count > maxLines)
            lines.Add($"… and {changes.Count - maxLines} more");

        var title = changes.Count == 1 ? "Container change" : $"{changes.Count} container changes";
        _notifyIcon.ShowBalloonTip(5000, title, string.Join("\n", lines), Forms.ToolTipIcon.Info);
    }

    private static string EngineLabel(string engineId) =>
        engineId.StartsWith("wsl:", StringComparison.Ordinal) ? $"WSL: {engineId[4..]}" : "Windows";

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
