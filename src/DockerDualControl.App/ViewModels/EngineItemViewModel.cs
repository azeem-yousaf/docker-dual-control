using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DockerDualControl.Core;

namespace DockerDualControl.App.ViewModels;

public sealed partial class EngineItemViewModel : ObservableObject
{
    public EngineItemViewModel(DiscoveredEngine discovered)
    {
        Engine = discovered.Engine;
        _isAvailable = discovered.IsAvailable;
        _version = discovered.Version;
        _serverOs = discovered.Os;
        _isInstalled = discovered.IsInstalled;
        Service = new DockerService(discovered.Engine);
    }

    public DockerEngine Engine { get; }
    public DockerService Service { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanSwitchMode))]
    private bool _isAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _version;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(SwitchModeText))]
    private string? _serverOs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isStarting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanSwitchMode))]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _isSwitching;

    /// <summary>Set once at discovery: Docker Desktop's Linux mode needs WSL installed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSwitchMode))]
    private bool _supportsModeSwitch;

    /// <summary>The engine is installed but not running, so a start can be offered.
    /// A mode switch takes the engine down transiently, so it must not surface Start.</summary>
    public bool CanStart => IsInstalled && !IsAvailable && !IsStarting && !IsSwitching;

    /// <summary>Docker Desktop is running and WSL is present, so container mode can be toggled.</summary>
    public bool CanSwitchMode =>
        Engine.Kind == EngineKind.Windows && IsAvailable && SupportsModeSwitch && !IsSwitching;

    /// <summary>The mode a switch would move to; "linux" or "windows".</summary>
    public string SwitchTargetOs => ServerOs == "windows" ? "linux" : "windows";

    public string SwitchModeText => SwitchTargetOs == "linux" ? "→ Linux" : "→ Windows";

    public string DisplayName => Engine.DisplayName;

    public string ShortName => Engine.Kind == EngineKind.Windows ? "Windows" : Engine.WslDistro!;

    public string StatusText =>
        IsSwitching ? "switching..."
        : !IsAvailable ? (IsStarting ? "starting..." : "not available")
        : Engine.Kind == EngineKind.Windows && ServerOs is not null
            ? $"v{Version} · {ServerOs} containers"
            : $"v{Version}";

    public Brush AccentBrush => Engine.Kind == EngineKind.Windows
        ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))
        : new SolidColorBrush(Color.FromRgb(0xE9, 0x54, 0x20));

    public Brush StatusBrush => IsAvailable
        ? new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F))
        : new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
}
