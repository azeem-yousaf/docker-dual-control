using System.Text.Json.Serialization;

namespace DockerDualControl.Core.Models;

public sealed record ContainerInfo
{
    [JsonPropertyName("ID")]
    public string Id { get; init; } = "";

    public string Names { get; init; } = "";
    public string Image { get; init; } = "";
    public string State { get; init; } = "";
    public string Status { get; init; } = "";
    public string Ports { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string RunningFor { get; init; } = "";

    [JsonIgnore]
    public bool IsRunning => State == "running";
}
