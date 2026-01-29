using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Core.Models;

public class PdfPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }

    [JsonIgnore]
    public string? DigitalText { get; set; }

    public List<string> ScreenshotFilepaths { get; set; } = [];

    public List<PdfPageProvider> Providers { get; set; } = [];
    
    [JsonIgnore]
    public object? PdfPigPage { get; set; }
}