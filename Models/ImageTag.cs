using System;
using System.Text.Json.Serialization;

namespace ExifEditor.Models;

[JsonConverter(typeof(JsonStringEnumConverter<TagPosition>))]
[Obsolete("Use LabelOffsetX/LabelOffsetY on ImageTag instead")]
public enum TagPosition
{
    Top,
    Bottom,
    Left,
    Right
}

public class ImageTag
{
    private const double DefaultOffsetX = 0.05;
    private const double DefaultOffsetY = -0.05;

    [JsonPropertyName("anchorX")]
    public double AnchorX { get; set; }

    [JsonPropertyName("anchorY")]
    public double AnchorY { get; set; }

    // Backwards-compat: older photos have the anchor stored under "x"/"y".
    // These setters are write-only on (de)serialization and feed into AnchorX/AnchorY.
    // JsonIgnore on the getter side via WhenWritingDefault keeps new files clean.
    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double LegacyX { get => 0; set => AnchorX = value; }

    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double LegacyY { get => 0; set => AnchorY = value; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("labelOffsetX")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LabelOffsetX { get; set; }

    [JsonPropertyName("labelOffsetY")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LabelOffsetY { get; set; }

    #pragma warning disable CS0612 // Suppress obsolete warning for backwards compatibility
    [JsonPropertyName("position")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TagPosition? Position { get; set; }
    #pragma warning restore CS0612

    [JsonIgnore]
    public double EffectiveLabelOffsetX
    {
        get
        {
            if (LabelOffsetX.HasValue) return LabelOffsetX.Value;
            #pragma warning disable CS0612
            return Position switch
            {
                TagPosition.Top => 0.0,
                TagPosition.Bottom => 0.0,
                TagPosition.Left => -0.08,
                TagPosition.Right => 0.08,
                _ => DefaultOffsetX
            };
            #pragma warning restore CS0612
        }
    }

    [JsonIgnore]
    public double EffectiveLabelOffsetY
    {
        get
        {
            if (LabelOffsetY.HasValue) return LabelOffsetY.Value;
            #pragma warning disable CS0612
            return Position switch
            {
                TagPosition.Top => -0.06,
                TagPosition.Bottom => 0.06,
                TagPosition.Left => 0.0,
                TagPosition.Right => 0.0,
                _ => DefaultOffsetY
            };
            #pragma warning restore CS0612
        }
    }

    [JsonIgnore]
    public string PositionDisplay => $"({AnchorX * 100:F0}%, {AnchorY * 100:F0}%)";
}
