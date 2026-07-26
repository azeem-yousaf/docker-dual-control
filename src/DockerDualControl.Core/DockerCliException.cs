namespace DockerDualControl.Core;

public sealed class DockerCliException : Exception
{
    public DockerCliException(string message) : base(message)
    {
    }

    public static DockerCliException FromResult(ProcessResult result)
    {
        var message = result.StdErr.Trim();
        if (message.Length == 0)
            message = $"docker exited with code {result.ExitCode}";
        return new DockerCliException(message);
    }
}
