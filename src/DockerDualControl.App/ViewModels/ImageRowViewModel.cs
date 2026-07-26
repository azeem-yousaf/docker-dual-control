using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;
using DockerDualControl.Core.Models;

namespace DockerDualControl.App.ViewModels;

public partial class ImageRowViewModel : ObservableObject
{
    private readonly ImagesViewModel _parent;
    private readonly EngineItemViewModel _engine;
    private readonly ImageInfo _info;

    [ObservableProperty]
    private bool _isWorking;

    public ImageRowViewModel(ImageInfo info, EngineItemViewModel engine, ImagesViewModel parent)
    {
        _info = info;
        _engine = engine;
        _parent = parent;
    }

    public string Repository => _info.Repository;
    public string Tag => _info.Tag;
    public string Size => _info.Size;
    public string CreatedSince => _info.CreatedSince;
    public string Reference => _info.Reference;

    public string EngineShortName => _engine.ShortName;
    public Brush EngineAccentBrush => _engine.AccentBrush;

    public string RowKey => $"{_engine.Engine.Id}/{Reference}";

    [RelayCommand]
    private async Task RunAsync()
    {
        if (DialogService.ShowRunContainerDialog(_parent.AvailableEngines, _engine, prefillImage: Reference))
            await _parent.RefreshAsync(clear: false, silent: true);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!DialogService.Confirm($"Delete image \"{Reference}\"?",
                $"The image will be removed from {_engine.DisplayName}."))
            return;

        _parent.SetBusy(RowKey, true);
        try
        {
            await _engine.Service.RemoveImageAsync(Reference);
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
