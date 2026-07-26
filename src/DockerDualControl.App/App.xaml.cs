using System.IO;
using System.Windows;

namespace DockerDualControl.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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

    private static void Log(Exception ex)
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
