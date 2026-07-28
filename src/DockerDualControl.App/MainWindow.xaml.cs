using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DockerDualControl.App.ViewModels;
using DockerDualControl.Core;
using Wpf.Ui.Appearance;

namespace DockerDualControl.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer;
    private TrayIcon? _trayIcon;
    private bool _exiting;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.AutoRefreshTickAsync();

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _refreshTimer.Stop();
            _trayIcon?.Dispose();
            _trayIcon = null;
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemThemeWatcher.Watch(this);
        SetUpTrayIcon();
        await _viewModel.DiscoverEnginesAsync();
        _refreshTimer.Start();
    }

    private void SetUpTrayIcon()
    {
        try
        {
            _trayIcon = new TrayIcon();
        }
        catch (Exception ex)
        {
            // No tray icon must not break the app; close then simply exits as before.
            App.Log(ex);
            return;
        }
        _trayIcon.OpenRequested += RestoreFromTray;
        _trayIcon.ExitRequested += () =>
        {
            _exiting = true;
            Application.Current.Shutdown();
        };
        _viewModel.Containers.StateChangesDetected += OnContainerStateChanges;
    }

    /// <summary>Close hides to the tray so the app keeps watching the engines;
    /// Exit in the tray menu (or a failed tray setup) really quits.</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting || _trayIcon is null)
            return;
        e.Cancel = true;
        Hide();
        _trayIcon.ShowFirstHideHint();
    }

    private void RestoreFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void OnContainerStateChanges(IReadOnlyList<ContainerStateChange> changes)
    {
        // Foreground use shows the live list already; notify only when the
        // window is out of sight (minimised or hidden in the tray).
        if (WindowState == WindowState.Minimized || !IsVisible)
            _trayIcon?.ShowContainerChanges(changes);
    }
}
