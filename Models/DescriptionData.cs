using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExifEditor.Models;

public class DescriptionData
{

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ImageTag>? Tags { get; set; }

    [JsonPropertyName("scanned")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Scanned { get; set; }

    public string? Serialize()
    {
        if (Tags is { Count: 0 })
            Tags = null;

        if (string.IsNullOrWhiteSpace(Description))
            Description = null;

        if (string.IsNullOrWhiteSpace(Scanned))
            Scanned = null;

        if (Description == null && Tags == null && Scanned == null)
            return null;

        return JsonSerializer.Serialize(this, DescriptionDataJsonContext.Default.DescriptionData);
    }

    public static DescriptionData Deserialize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return new DescriptionData();

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("{"))
        {
            try
            {
                return JsonSerializer.Deserialize(trimmed, DescriptionDataJsonContext.Default.DescriptionData)
                       ?? new DescriptionData();
            }
            catch
            {
                return new DescriptionData { Description = raw };
            }
        }

        return new DescriptionData { Description = raw };
    }
}

[JsonSerializable(typeof(DescriptionData))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false)]
internal partial class DescriptionDataJsonContext : JsonSerializerContext
{
}
