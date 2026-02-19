using SkiaSharp;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IInternalPdfDocument : IDisposable
{
    public List<InternalPdfDocumentPage> GetPages();

    public SKBitmap GetPageAsSKBitmap(int pageNumber, float quality);
}