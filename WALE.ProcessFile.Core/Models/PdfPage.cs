using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Core.Models;

public class PdfPage
{
    public int Number { get; set; }

    public int NumberOfImages { get; set; }

    // In PDF points, same coordinate space as DocumentLineWordCoordinates - lets a consumer
    // (the portal's PDF-highlight overlay) normalise a matched word's rectangle to a
    // percentage of the page regardless of the rendered image's actual pixel size. 0 when
    // unknown (not populated by every IInternalPdfDocumentPage implementation).
    public double Width { get; set; }

    public double Height { get; set; }

    [JsonIgnore]
    public string? DigitalText { get; set; }

    // There are multiple as (at time of writing) one from PdfPig and one from Docnet
    public List<string> ScreenshotFilepaths { get; set; } = [];

    public List<PdfPageProvider> Providers { get; set; } = [];
    
    [JsonIgnore]
    public IInternalPdfDocumentPage? InternalPage { get; set; }

    public bool LikelyMapPage { get; set; }
}