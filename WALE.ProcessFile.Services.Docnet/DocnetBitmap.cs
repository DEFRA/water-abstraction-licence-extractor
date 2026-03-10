using Docnet.Core.Models;
using SkiaSharp;

namespace WALE.ProcessFile.Services.Docnet;

public class DocnetBitmap
{
    public SKBitmap GetPageAsSkBitmap(string pdfFilePath, PageDimensions pageDimensions, int pageNumber)
    {
        var docLibInstance = new DocLibInstance();
        using var docReader = docLibInstance.GetDocReader(pdfFilePath, pageDimensions);

        using var pageReader = docReader.GetPageReader(pageNumber - 1);
        var rawBytes = pageReader.GetImage();

        for (var i = 0; i < rawBytes.Length / 4; i++)
        {
            var j = i * 4;
            var alpha = rawBytes[j];
            var red = rawBytes[j + 1];
            var green = rawBytes[j + 2];
            var blue = rawBytes[j + 3];

            if (alpha != 0 || red != 0 || green != 0 || blue != 0) continue;

            rawBytes[j] = byte.MaxValue;
            rawBytes[j + 1] = byte.MaxValue;
            rawBytes[j + 2] = byte.MaxValue;
            rawBytes[j + 3] = byte.MaxValue;
        }

        var skImage = SKImage.FromPixelCopy(
            new SKImageInfo(
                pageReader.GetPageWidth(),
                pageReader.GetPageHeight(),
                SKColorType.Bgra8888
            ),
            rawBytes);

        return SKBitmap.FromImage(skImage);
    }
}