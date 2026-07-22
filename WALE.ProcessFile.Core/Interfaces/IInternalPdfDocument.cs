using SkiaSharp;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IInternalPdfDocument : IDisposable
{
    public List<IInternalPdfDocumentPage> GetPages();

    public SKBitmap GetPageAsSkBitmap(int pageNumber, float scale);
    
    public Stream FileStream { get; }
    
    public long SizeBytes { get; }
}