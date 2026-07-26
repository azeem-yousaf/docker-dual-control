using DockerDualControl.App.ViewModels;

namespace DockerDualControl.App.Views;

public partial class RunContainerDialog : Wpf.Ui.Controls.FluentWindow
{
    public RunContainerDialog(RunContainerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
