using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ImageMetadataPageScreenshot
{
    [JsonPropertyName("providerName")]
    public string? ProviderName { get; set; }

    [JsonPropertyName("imageReference")] 
    public string? ImageReference { get; set; }
}