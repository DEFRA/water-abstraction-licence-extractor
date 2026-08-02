using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class ImageMetadata
{
    [JsonPropertyName("pages")]
    public List<ImageMetadataPage> Pages { get; set; } = [];
}