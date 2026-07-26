namespace DockerDualControl.Core;

public enum EngineKind
{
    Windows,
    Wsl,
}

public sealed record DockerEngine(string Id, string DisplayName, EngineKind Kind, string? WslDistro)
{
    public const string WindowsPipeHost = "npipe:////./pipe/docker_engine";

    public static DockerEngine Windows() =>
        new("windows", "Windows Engine", EngineKind.Windows, null);

    public static DockerEngine ForWslDistro(string distro) =>
        new($"wsl:{distro}", $"WSL: {distro}", EngineKind.Wsl, distro);

    public (string FileName, List<string> Args) BuildCommand(IEnumerable<string> dockerArgs)
    {
        var args = new List<string>();
        string fileName;
        if (Kind == EngineKind.Windows)
        {
            fileName = "docker";
            args.Add("-H");
            args.Add(WindowsPipeHost);
        }
        else
        {
            fileName = "wsl.exe";
            args.Add("-d");
            args.Add(WslDistro!);
            // -e executes directly instead of via the distro's shell, so docker
            // args with shell-special characters (|, $, quotes) pass through verbatim.
            args.Add("-e");
            args.Add("docker");
        }
        args.AddRange(dockerArgs);
        return (fileName, args);
    }
}
