using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigInternalPdfDocumentPage(Page page) : IInternalPdfDocumentPage
{
    public int Number { get; set; }
    public int NumberOfImages { get; set; }
    public string? Text { get; set; }
    public double Width { get; set; } = page.Width;
    public double Height { get; set; } = page.Height;
    public object UnderlyingObject { get; set; } = page;

    public List<IInternalPdfImage> GetImages()
    {
        return page
            .GetImages()
            .Select(IInternalPdfImage (img) => new PdfPigInternalPdfImage(img))
            .ToList();
    }
}