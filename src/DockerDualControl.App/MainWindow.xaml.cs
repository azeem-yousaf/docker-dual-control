using System.Windows;
using System.Windows.Threading;
using DockerDualControl.App.ViewModels;
using Wpf.Ui.Appearance;

namespace DockerDualControl.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await _viewModel.AutoRefreshTickAsync();

        Loaded += OnLoaded;
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemThemeWatcher.Watch(this);
        await _viewModel.DiscoverEnginesAsync();
        _refreshTimer.Start();
    }
}
