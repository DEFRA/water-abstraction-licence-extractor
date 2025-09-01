using System.IO.Compression;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<string?> SaveImageBytesAsync(int imageNumber, int pageNumber, string cacheFolder)
    {
        const string pngExtension = "png";
        const string bmpExtension = "bmp";
        const string jpgExtension = "jpg";
        
        try
        {
            if (imageData.TryGetPng(out var bytes))
            {
                await File.WriteAllBytesAsync(
                    GetImageFilepath(imageNumber, pageNumber, cacheFolder, true, pngExtension),
                    bytes);
                
                return pngExtension;
            }

            if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                await File.WriteAllBytesAsync(
                    GetImageFilepath(imageNumber, pageNumber, cacheFolder, true, bmpExtension),
                    bytesMemory.ToArray());
                
                return bmpExtension;
            }

            var bytesSpanAry = imageData.RawBytes.ToArray();
            if (bytesSpanAry.Length == 0)
            {
                throw new Exception("Cannot get bytes via either method");
            }

            await File.WriteAllBytesAsync(
                GetImageFilepath(imageNumber, pageNumber, cacheFolder, true, jpgExtension),
                bytesSpanAry);
            
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

    public string GetImageFilepath(int imageNumber, int pageNumber, string cacheFolder, bool createDirectory, string extension)
    {
        var outputFolderFull = $"{cacheFolder}/PdfPig/Images";
        
        if (createDirectory)
        {
            Directory.CreateDirectory(outputFolderFull);
        }
        
        return $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.{extension}";
    }

    public static byte[] Deflate(byte[] input) // TODO use again
    {
        var cutInput = new byte[input.Length - 2];
        Array.Copy(input, 2, cutInput, 0, cutInput.Length);

        var str = System.Text.Encoding.Default.GetString(input);
        
        var stream = new MemoryStream();

        using var compressStream = new MemoryStream(cutInput);
        using var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress);
        
        decompressor.CopyTo(stream);
        return stream.ToArray();
    }
}