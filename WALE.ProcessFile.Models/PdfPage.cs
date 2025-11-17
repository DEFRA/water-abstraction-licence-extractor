using System.Text.Json.Serialization;

namespace WALE.ProcessFile.Models;

public class PdfPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }

    [JsonIgnore]
    public string? Text { get; set; }

    public string GetImageFilepath(string serviceName)
    {
        return $"{serviceName}/Images/page-{Number}.png";
    }
    
    public string? ImageFilepath { get; set; }

    public List<PdfPageProvider> Providers { get; set; } = [];
    
    [JsonIgnore]
    public object? PdfPigPage { get; set; }
}