using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExifEditor.Models;

public class DescriptionData
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ImageTag>? Tags { get; set; }

    public string? Serialize()
    {
        if (Tags is { Count: 0 })
            Tags = null;

        if (string.IsNullOrWhiteSpace(Description))
            Description = null;

        if (Description == null && Tags == null)
            return null;

        return JsonSerializer.Serialize(this, SerializerOptions);
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
                return JsonSerializer.Deserialize<DescriptionData>(trimmed, SerializerOptions)
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
