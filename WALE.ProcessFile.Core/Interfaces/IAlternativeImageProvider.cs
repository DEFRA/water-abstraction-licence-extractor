using SkiaSharp;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IAlternativeImageProvider
{
    public SKBitmap GetPageAsSkBitmap(
        Stream fileStream,
        int pageDimensionWidth,
        int pageDimensionHeight,
        int pageNumber);
}