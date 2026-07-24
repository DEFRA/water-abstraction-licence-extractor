using System.Collections.Concurrent;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using SkiaSharp;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Docnet;

public class DocnetAlternativeImageProvider : IAlternativeImageProvider
{
    private IDocReader? _docReader;
    
    public SKBitmap GetPageAsSkBitmap(
        Stream fileStream,
        int pageDimensionWidth,
        int pageDimensionHeight,
        int pageNumber)
    {
        if (_docReader == null)
        {
            var docLibInstance = new DocLibInstance();

            _docReader = docLibInstance.GetDocReader(
                fileStream,
                new PageDimensions(pageDimensionWidth, pageDimensionHeight));
        }

        using var pageReader = _docReader.GetPageReader(pageNumber - 1);
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