using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ImageMetadata
{
    [JsonPropertyName("pages")]
    public List<ImageMetadataPage> Pages { get; set; } = [];
}