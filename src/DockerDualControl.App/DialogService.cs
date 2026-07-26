using System.Windows;
using DockerDualControl.App.ViewModels;
using DockerDualControl.App.Views;
using DockerDualControl.Core;

namespace DockerDualControl.App;

public static class DialogService
{
    /// <summary>Shows the run-container dialog; returns true when a container was started.</summary>
    public static bool ShowRunContainerDialog(
        IReadOnlyList<EngineItemViewModel> engines,
        EngineItemViewModel selectedEngine,
        string? prefillImage)
    {
        var viewModel = new RunContainerViewModel(engines, selectedEngine, prefillImage);
        var dialog = new RunContainerDialog(viewModel) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
        return viewModel.Succeeded;
    }

    public static void ShowLogsWindow(DockerService service, string containerId, string containerName)
    {
        var window = new LogsWindow(new LogsViewModel(service, containerId, containerName))
        {
            Owner = Application.Current.MainWindow,
        };
        window.Show();
    }

    public static bool Confirm(string title, string message)
    {
        return MessageBox.Show(
                   Application.Current.MainWindow!,
                   message,
                   title,
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}
