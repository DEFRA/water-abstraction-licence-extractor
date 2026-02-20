using SkiaSharp;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigInternalPdfDocument(PdfDocument pdfDocument) : IInternalPdfDocument
{
    public List<IInternalPdfDocumentPage> GetPages()
    {
        return pdfDocument
            .GetPages()
            .Select(IInternalPdfDocumentPage (p) => new PdfPigInternalPdfDocumentPage(p)
            {
                Number = p.Number,
                NumberOfImages = p.NumberOfImages,
                Text = p.Text
            })
            .ToList();
    }

    public SKBitmap GetPageAsSKBitmap(int pageNumber, float scale)
    {
        return pdfDocument.GetPageAsSKBitmap(pageNumber, scale);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}