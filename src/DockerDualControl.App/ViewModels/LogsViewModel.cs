using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerDualControl.Core;

namespace DockerDualControl.App.ViewModels;

public partial class LogsViewModel : ObservableObject, IDisposable
{
    private readonly Process _process;

    [ObservableProperty]
    private bool _followTail = true;

    public string Title { get; }

    public event Action<string>? LineReceived;

    public LogsViewModel(DockerService service, string containerId, string containerName)
    {
        Title = $"Logs — {containerName} ({service.Engine.DisplayName})";
        _process = service.StartLogsProcess(containerId);
        _ = PumpAsync(_process.StandardOutput);
        _ = PumpAsync(_process.StandardError);
    }

    private async Task PumpAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
                LineReceived?.Invoke(line);
        }
        catch (ObjectDisposedException)
        {
            // window closed while streaming
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // process already gone
        }
        _process.Dispose();
    }
}
