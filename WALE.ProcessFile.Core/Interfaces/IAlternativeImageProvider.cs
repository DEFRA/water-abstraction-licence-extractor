using SkiaSharp;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IAlternativeImageProvider
{
    public SKBitmap GetPageAsSkBitmap(
        IFileService fileService,
        string pdfFilename,
        int pageDimensionWidth,
        int pageDimensionHeight,
        int pageNumber);
}