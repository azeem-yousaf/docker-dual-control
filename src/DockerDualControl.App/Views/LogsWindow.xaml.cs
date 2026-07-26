using System.Text;
using DockerDualControl.App.ViewModels;

namespace DockerDualControl.App.Views;

public partial class LogsWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly LogsViewModel _viewModel;
    private readonly StringBuilder _pending = new();
    private readonly object _pendingLock = new();
    private readonly System.Windows.Threading.DispatcherTimer _flushTimer;

    public LogsWindow(LogsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.LineReceived += OnLineReceived;

        // Batch UI appends: docker can emit thousands of lines per second.
        _flushTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150),
        };
        _flushTimer.Tick += (_, _) => Flush();
        _flushTimer.Start();

        Closed += (_, _) =>
        {
            _flushTimer.Stop();
            viewModel.LineReceived -= OnLineReceived;
            viewModel.Dispose();
        };
    }

    private void OnLineReceived(string line)
    {
        lock (_pendingLock)
            _pending.AppendLine(line);
    }

    private void Flush()
    {
        string chunk;
        lock (_pendingLock)
        {
            if (_pending.Length == 0)
                return;
            chunk = _pending.ToString();
            _pending.Clear();
        }
        LogText.AppendText(chunk);
        if (_viewModel.FollowTail)
            LogText.ScrollToEnd();
    }
}
