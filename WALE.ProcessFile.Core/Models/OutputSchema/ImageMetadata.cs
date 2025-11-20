using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Models.OutputSchema;

public class ImageMetadata
{
    [JsonPropertyName("pages")]
    public List<ImageMetadataPage> Pages { get; set; } = [];
}