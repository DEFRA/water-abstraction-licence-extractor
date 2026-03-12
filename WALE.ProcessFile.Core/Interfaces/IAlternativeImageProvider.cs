using SkiaSharp;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IAlternativeImageProvider
{
    public Task<SKBitmap> GetPageAsSkBitmapAsync(
        IFileService fileService,
        string pdfFilename,
        int pageDimensionWidth,
        int pageDimensionHeight,
        int pageNumber);
}