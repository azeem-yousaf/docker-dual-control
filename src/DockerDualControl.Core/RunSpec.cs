namespace DockerDualControl.Core;

public sealed record PortMapping(string Host, string Container);
public sealed record EnvVar(string Key, string Value);
public sealed record VolumeMapping(string Host, string Container);

public sealed class RunSpec
{
    public required string Image { get; init; }
    public string? Name { get; init; }
    public List<PortMapping> Ports { get; } = new();
    public List<EnvVar> Env { get; } = new();
    public List<VolumeMapping> Volumes { get; } = new();
    public string? Command { get; init; }

    public List<string> ToDockerArgs()
    {
        var args = new List<string> { "run", "-d" };

        if (!string.IsNullOrWhiteSpace(Name))
        {
            args.Add("--name");
            args.Add(Name.Trim());
        }
        foreach (var p in Ports)
        {
            args.Add("-p");
            args.Add($"{p.Host}:{p.Container}");
        }
        foreach (var e in Env)
        {
            args.Add("-e");
            args.Add($"{e.Key}={e.Value}");
        }
        foreach (var v in Volumes)
        {
            args.Add("-v");
            args.Add($"{v.Host}:{v.Container}");
        }

        args.Add(Image);

        if (!string.IsNullOrWhiteSpace(Command))
            args.AddRange(SplitCommand(Command));

        return args;
    }

    /// <summary>Splits on whitespace but keeps quoted ('…' or "…") segments intact.</summary>
    internal static List<string> SplitCommand(string command)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        char? quote = null;

        foreach (var ch in command)
        {
            if (quote is null && ch is '\'' or '"')
            {
                quote = ch;
                current.Append(ch);
            }
            else if (quote == ch)
            {
                quote = null;
                current.Append(ch);
            }
            else if (quote is null && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
        return tokens;
    }
}
