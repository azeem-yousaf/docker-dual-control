using System.IO;
using System.Threading;
using System.Windows;

namespace DockerDualControl.App;

public partial class App : Application
{
    // Session-local (per user session): two users on one machine may each run one.
    private const string MutexName = @"Local\DockerDualControl.SingleInstance";
    private const string ShowEventName = @"Local\DockerDualControl.ShowExisting";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!ClaimSingleInstance())
        {
            // An instance is already running: it has been signaled to show itself.
            // Shut down before the StartupUri window (and tray icon) get created.
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            MessageBox.Show(args.Exception.Message, "Docker Dual Control — unexpected error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log(args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>True if this process is (now) the single instance. On false, the
    /// existing instance has been told to bring its window forward. Any failure in
    /// the kernel-object plumbing falls back to launching normally: a stray second
    /// instance beats a refusal to start.</summary>
    private bool ClaimSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var isFirst);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

            if (!isFirst)
            {
                _showEvent.Set();
                return false;
            }

            var thread = new Thread(WaitForShowRequests) { IsBackground = true };
            thread.Start();
            return true;
        }
        catch (Exception ex)
        {
            Log(ex);
            return true;
        }
    }

    private void WaitForShowRequests()
    {
        while (_showEvent!.WaitOne())
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (MainWindow is MainWindow main)
                    main.RestoreFromTray();
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showEvent?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    internal static void Log(Exception ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "DockerDualControl.error.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch
        {
            // logging must never crash the app
        }
    }
}
