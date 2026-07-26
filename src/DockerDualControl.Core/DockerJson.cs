using System.Text.Json;

namespace DockerDualControl.Core;

public static class DockerJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Parses `--format "{{json .}}"` output: one JSON object per line.</summary>
    public static List<T> ParseLines<T>(string stdout)
    {
        var result = new List<T>();
        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            var item = JsonSerializer.Deserialize<T>(line, Options);
            if (item is not null)
                result.Add(item);
        }
        return result;
    }
}
