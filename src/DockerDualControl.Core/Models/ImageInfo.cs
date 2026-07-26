using System.Text.Json.Serialization;

namespace DockerDualControl.Core.Models;

public sealed record ImageInfo
{
    [JsonPropertyName("ID")]
    public string Id { get; init; } = "";

    public string Repository { get; init; } = "";
    public string Tag { get; init; } = "";
    public string Size { get; init; } = "";
    public string CreatedSince { get; init; } = "";

    [JsonIgnore]
    public string Reference => Tag is "" or "<none>" ? Repository : $"{Repository}:{Tag}";
}
