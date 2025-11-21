using System.IO.Compression;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<string?> SaveImageBytesAsync(string folderPath, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId)
    {
        const string pngExtension = "png";
        const string bmpExtension = "bmp";
        const string jpgExtension = "jpg";
        
        try
        {
            if (imageData.TryGetPng(out var bytes))
            {
                await cacheService.SaveImageOnPageAsync(bytes, folderPath, PdfDataExtractorService.Name, imageNumber, pageNumber, pngExtension, processRunId);
                return pngExtension;
            }

            if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                await cacheService.SaveImageOnPageAsync(bytesMemory.ToArray(), folderPath, PdfDataExtractorService.Name, imageNumber, pageNumber, bmpExtension, processRunId);
                return bmpExtension;
            }

            var bytesSpanAry = imageData.RawBytes.ToArray();
            if (bytesSpanAry.Length == 0)
            {
                throw new Exception("Cannot get bytes via either method");
            }

            await cacheService.SaveImageOnPageAsync(bytesSpanAry, folderPath, PdfDataExtractorService.Name, imageNumber, pageNumber, jpgExtension, processRunId);
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