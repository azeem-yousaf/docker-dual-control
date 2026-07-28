using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;

namespace DockerDualControl.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<EngineItemViewModel> Engines { get; } = new();

    public ContainersViewModel Containers { get; }
    public ImagesViewModel Images { get; }

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private int _selectedTabIndex;

    public MainViewModel()
    {
        Containers = new ContainersViewModel(this);
        Images = new ImagesViewModel(this);
    }

    public IReadOnlyList<EngineItemViewModel> AvailableEngines =>
        Engines.Where(e => e.IsAvailable).ToList();

    public bool NoEnginesAvailable => AvailableEngines.Count == 0;

    partial void OnSelectedTabIndexChanged(int value) => _ = ReloadActiveTabAsync(clear: false);

    [RelayCommand]
    public async Task DiscoverEnginesAsync()
    {
        IsDiscovering = true;
        try
        {
            var discovered = await EngineDiscovery.DiscoverAsync();
            var wslInstalled = await EngineControl.IsWslInstalledAsync();

            Engines.Clear();
            foreach (var d in discovered)
            {
                var item = new EngineItemViewModel(d);
                if (item.Engine.Kind == EngineKind.Windows)
                    item.SupportsModeSwitch = wslInstalled;
                Engines.Add(item);
            }

            OnPropertyChanged(nameof(AvailableEngines));
            OnPropertyChanged(nameof(NoEnginesAvailable));
        }
        finally
        {
            IsDiscovering = false;
        }
        await ReloadActiveTabAsync(clear: true);
    }

    public async Task AutoRefreshTickAsync()
    {
        // Re-probe engine status alongside the tab reload so the status chips
        // track engines coming online/offline, not just container state.
        var statusProbe = RefreshEngineStatusAsync();
        if (!NoEnginesAvailable)
        {
            // Containers refresh every tick regardless of the active tab (or the
            // window being hidden in the tray): the start/stop notifications need
            // a continuous stream of snapshots to diff.
            await Containers.RefreshAsync(clear: false, silent: true);
            if (SelectedTabIndex != 0)
                await ReloadActiveTabAsync(clear: false, silent: true);
        }
        await statusProbe;
    }

    private bool _statusProbeInProgress;

    public async Task RefreshEngineStatusAsync()
    {
        if (_statusProbeInProgress || Engines.Count == 0)
            return;
        _statusProbeInProgress = true;
        try
        {
            var results = await Task.WhenAll(Engines.Select(async engine =>
                (engine, ping: await engine.Service.PingAsync())));

            var availabilityChanged = false;
            foreach (var (engine, ping) in results)
            {
                if (engine.IsAvailable != (ping is not null))
                    availabilityChanged = true;
                engine.Version = ping?.Version;
                engine.ServerOs = ping?.Os;
                engine.IsAvailable = ping is not null;
                if (ping is not null)
                    engine.IsInstalled = true; // it responded, so it certainly exists
            }

            if (availabilityChanged)
            {
                OnPropertyChanged(nameof(AvailableEngines));
                OnPropertyChanged(nameof(NoEnginesAvailable));
            }
        }
        finally
        {
            _statusProbeInProgress = false;
        }
    }

    [RelayCommand]
    private async Task StartEngineAsync(EngineItemViewModel engine)
    {
        if (engine.IsStarting || engine.IsAvailable)
            return;
        engine.IsStarting = true;
        try
        {
            await EngineControl.StartEngineAsync(engine.Engine);

            // The start command returning does not mean the daemon is ready
            // (Docker Desktop in particular takes a while); poll until it answers.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                var ping = await engine.Service.PingAsync();
                if (ping is not null)
                {
                    engine.Version = ping.Version;
                    engine.ServerOs = ping.Os;
                    engine.IsAvailable = true;
                    engine.IsInstalled = true;
                    OnPropertyChanged(nameof(AvailableEngines));
                    OnPropertyChanged(nameof(NoEnginesAvailable));
                    await ReloadActiveTabAsync(clear: false);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            ReportEngineError(engine, "engine did not come online within 90 seconds.");
        }
        catch (Exception ex) when (ex is DockerCliException or TimeoutException)
        {
            ReportEngineError(engine, ex.Message);
        }
        finally
        {
            engine.IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task SwitchEngineModeAsync(EngineItemViewModel engine)
    {
        if (!engine.CanSwitchMode)
            return;
        var target = engine.SwitchTargetOs;
        engine.IsSwitching = true;
        try
        {
            await EngineControl.SwitchEngineModeAsync(target);

            // The daemon restarts in the new mode; poll until it reports the target OS.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                var ping = await engine.Service.PingAsync();
                if (ping?.Os == target)
                {
                    engine.Version = ping.Version;
                    engine.ServerOs = ping.Os;
                    engine.IsAvailable = true;
                    await ReloadActiveTabAsync(clear: false);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            ReportEngineError(engine, $"engine did not come back in {target} mode within 90 seconds.");
        }
        catch (Exception ex) when (ex is DockerCliException or TimeoutException)
        {
            ReportEngineError(engine, ex.Message);
        }
        finally
        {
            engine.IsSwitching = false;
        }
    }

    private void ReportEngineError(EngineItemViewModel engine, string message)
    {
        var text = $"{engine.DisplayName}: {message}";
        if (SelectedTabIndex == 0)
            Containers.ReportError(text);
        else
            Images.ReportError(text);
    }

    private async Task ReloadActiveTabAsync(bool clear, bool silent = false)
    {
        if (SelectedTabIndex == 0)
            await Containers.RefreshAsync(clear, silent);
        else
            await Images.RefreshAsync(clear, silent);
    }
}
