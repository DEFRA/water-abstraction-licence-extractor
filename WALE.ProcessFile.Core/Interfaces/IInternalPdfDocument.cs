using SkiaSharp;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IInternalPdfDocument : IDisposable
{
    public List<IInternalPdfDocumentPage> GetPages();

    public SKBitmap GetPageAsSKBitmap(int pageNumber, float scale);
}