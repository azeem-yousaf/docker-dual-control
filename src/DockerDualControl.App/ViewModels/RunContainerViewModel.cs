using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DockerDualControl.Core;

namespace DockerDualControl.App.ViewModels;

public partial class RunContainerViewModel : ObservableObject
{
    public IReadOnlyList<EngineItemViewModel> Engines { get; }

    [ObservableProperty]
    private EngineItemViewModel _selectedEngine;

    [ObservableProperty]
    private string _image = "";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _portsText = "";

    [ObservableProperty]
    private string _envText = "";

    [ObservableProperty]
    private string _volumesText = "";

    [ObservableProperty]
    private string _command = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isRunning;

    public bool Succeeded { get; private set; }

    public event EventHandler? CloseRequested;

    public RunContainerViewModel(
        IReadOnlyList<EngineItemViewModel> engines,
        EngineItemViewModel selectedEngine,
        string? prefillImage)
    {
        Engines = engines;
        _selectedEngine = selectedEngine;
        if (prefillImage is not null)
            Image = prefillImage;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        ErrorMessage = null;

        RunSpec spec;
        try
        {
            spec = BuildSpec();
        }
        catch (FormatException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        IsRunning = true;
        try
        {
            await SelectedEngine.Service.RunContainerAsync(spec);
            Succeeded = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is DockerCliException or TimeoutException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private RunSpec BuildSpec()
    {
        if (string.IsNullOrWhiteSpace(Image))
            throw new FormatException("Image is required — for example \"nginx:latest\".");

        var spec = new RunSpec
        {
            Image = Image.Trim(),
            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
            Command = string.IsNullOrWhiteSpace(Command) ? null : Command.Trim(),
        };

        foreach (var line in NonEmptyLines(PortsText))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                throw new FormatException($"Port \"{line}\" should look like host:container — for example \"8080:80\".");
            spec.Ports.Add(new PortMapping(parts[0], parts[1]));
        }

        foreach (var line in NonEmptyLines(EnvText))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2 || parts[0].Length == 0)
                throw new FormatException($"Environment variable \"{line}\" should look like KEY=value.");
            spec.Env.Add(new EnvVar(parts[0], parts[1]));
        }

        foreach (var line in NonEmptyLines(VolumesText))
        {
            var idx = line.LastIndexOf(':');
            if (idx <= 0 || idx == line.Length - 1)
                throw new FormatException($"Volume \"{line}\" should look like host-path:container-path.");
            spec.Volumes.Add(new VolumeMapping(line[..idx], line[(idx + 1)..]));
        }

        return spec;
    }

    private static IEnumerable<string> NonEmptyLines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
}
