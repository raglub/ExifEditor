using System.Text.Json.Serialization;

namespace ExifEditor.Models;

public class ImageTag
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonIgnore]
    public string PositionDisplay => $"({X * 100:F0}%, {Y * 100:F0}%)";
}
