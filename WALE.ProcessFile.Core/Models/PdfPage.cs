using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Core.Models;

public class PdfPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }

    [JsonIgnore]
    public string? DigitalText { get; set; }

    // There are multiple as (at time of writing) one from PdfPig and one from Docnet
    public List<string> ScreenshotFilepaths { get; set; } = [];

    public List<PdfPageProvider> Providers { get; set; } = [];
    
    [JsonIgnore]
    public IInternalPdfDocumentPage? InternalPage { get; set; }
}