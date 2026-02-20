using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigInternalPdfDocumentPage(Page page) : IInternalPdfDocumentPage
{
    public int Number { get; set; }
    public int NumberOfImages { get; set; }
    public string? Text { get; set; }
    public object UnderlyingObject { get; set; } = page;

    public List<IInternalPdfImage> GetImages()
    {
        return page
            .GetImages()
            .Select(IInternalPdfImage (i) => new PdfPigInternalPdfImage(i))
            .ToList();
    }
}