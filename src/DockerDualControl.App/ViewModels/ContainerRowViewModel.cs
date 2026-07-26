using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.App.ViewModels;

public partial class ContainerRowViewModel : ObservableObject
{
    private readonly ContainersViewModel _parent;
    private readonly EngineItemViewModel _engine;
    private readonly ContainerInfo _info;

    [ObservableProperty]
    private bool _isWorking;

    [ObservableProperty]
    private bool _isVisible = true;

    public ContainerRowViewModel(ContainerInfo info, EngineItemViewModel engine, ContainersViewModel parent)
    {
        _info = info;
        _engine = engine;
        _parent = parent;
    }

    public string Id => _info.Id;
    public string ShortId => _info.Id.Length > 12 ? _info.Id[..12] : _info.Id;
    public string Names => _info.Names;
    public string Image => _info.Image;
    public string State => _info.State;
    public string Status => _info.Status;
    public string Ports => _info.Ports;
    public bool IsRunning => _info.IsRunning;
    public bool IsNotRunning => !_info.IsRunning;

    public string EngineShortName => _engine.ShortName;
    public Brush EngineAccentBrush => _engine.AccentBrush;

    public string RowKey => $"{_engine.Engine.Id}/{Id}";

    [RelayCommand]
    private Task StartAsync() => ExecuteAsync(s => s.StartContainerAsync(Id));

    [RelayCommand]
    private Task StopAsync() => ExecuteAsync(s => s.StopContainerAsync(Id));

    [RelayCommand]
    private Task RestartAsync() => ExecuteAsync(s => s.RestartContainerAsync(Id));

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (!DialogService.Confirm($"Delete container \"{Names}\"?",
                $"The container will be removed from {_engine.DisplayName}. This can't be undone."))
            return;
        await ExecuteAsync(s => s.RemoveContainerAsync(Id));
    }

    [RelayCommand]
    private void OpenLogs() => DialogService.ShowLogsWindow(_engine.Service, Id, Names);

    [RelayCommand]
    private void OpenShell()
    {
        try
        {
            _engine.Service.StartShellProcess(Id, _engine.ServerOs);
        }
        catch (Exception ex) when (ex is DockerCliException or System.ComponentModel.Win32Exception)
        {
            _parent.ReportError($"{_engine.DisplayName}: {ex.Message}");
        }
    }

    private async Task ExecuteAsync(Func<DockerService, Task> action)
    {
        _parent.SetBusy(RowKey, true);
        try
        {
            await action(_engine.Service);
        }
        catch (Exception ex) when (ex is DockerCliException or TimeoutException)
        {
            _parent.ReportError($"{_engine.DisplayName}: {ex.Message}");
        }
        finally
        {
            _parent.SetBusy(RowKey, false);
            await _parent.RefreshAsync(clear: false, silent: true);
        }
    }
}
