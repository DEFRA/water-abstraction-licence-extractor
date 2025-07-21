using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Services.Models;

public class ImageMetadataPage
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("imageFilename")]
    public string? ImageFilename { get; set; }

    [JsonPropertyName("imageFiles")]    
    public List<string> ImageFiles { get; set; } = [];
}