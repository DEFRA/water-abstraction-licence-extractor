using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Models.OutputSchema;

public class ImageMetadataPage
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    public string? ImageReference { get; set; }

    [JsonPropertyName("imageFiles")] 
    public List<string> Images { get; set; } = [];
}