using SkiaSharp;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IAlternativeImageProvider
{
    public Task<SKBitmap> GetPageAsSkBitmapAsync(
        Stream fileStream,
        string pdfFilename,
        int pageDimensionWidth,
        int pageDimensionHeight,
        int pageNumber);
}