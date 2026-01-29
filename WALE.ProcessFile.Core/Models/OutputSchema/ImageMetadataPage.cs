using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ImageMetadataPage
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    public List<(string ProviderName, string? ImageReference)> ImageReferences { get; set; } = [];

    [JsonPropertyName("imageFiles")] 
    public List<string> Images { get; set; } = [];
}