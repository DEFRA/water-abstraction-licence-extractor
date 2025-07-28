using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Services.Models;

public class ImageMetadata
{
    [JsonPropertyName("pages")]
    public List<ImageMetadataPage> Pages { get; set; } = [];
}