using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class ImageMetadataPage
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("screenshotReferences")] 
    public List<ImageMetadataPageScreenshot> ScreenshotReferences { get; set; } = [];

    [JsonPropertyName("imageFiles")] 
    public List<string> Images { get; set; } = [];
}