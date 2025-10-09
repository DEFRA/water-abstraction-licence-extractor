using System.IO.Compression;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<string?> SaveImageBytesAsync(string folderPath, int imageNumber, int pageNumber, ICacheService cacheService)
    {
        const string pngExtension = "png";
        const string bmpExtension = "bmp";
        const string jpgExtension = "jpg";
        
        try
        {
            if (imageData.TryGetPng(out var bytes))
            {
                await cacheService.SaveImageAsync(bytes, folderPath, imageNumber, pageNumber, pngExtension);
                return pngExtension;
            }

            if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                await cacheService.SaveImageAsync(bytesMemory.ToArray(), folderPath, imageNumber, pageNumber, bmpExtension);
                return bmpExtension;
            }

            var bytesSpanAry = imageData.RawBytes.ToArray();
            if (bytesSpanAry.Length == 0)
            {
                throw new Exception("Cannot get bytes via either method");
            }

            await cacheService.SaveImageAsync(bytesSpanAry, folderPath, imageNumber, pageNumber, jpgExtension);
            return jpgExtension;
        }
        catch (Exception exception)
        {
            if (exception is IOException)
            {
                return null;
            }

            // TODO should work out when need to deflate
            //bytesAry = Deflate(bytesAry);

            return null;
        }
    }

    public static byte[] Deflate(byte[] input)
    {
        var cutInput = new byte[input.Length - 2];
        Array.Copy(input, 2, cutInput, 0, cutInput.Length);

        var stream = new MemoryStream();

        using var compressStream = new MemoryStream(cutInput);
        using var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress);
        
        decompressor.CopyTo(stream);
        return stream.ToArray();
    }
}