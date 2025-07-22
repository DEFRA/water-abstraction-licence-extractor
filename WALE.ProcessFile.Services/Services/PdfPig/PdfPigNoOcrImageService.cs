using System.IO.Compression;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<string> SaveImageBytesAsync(int imageNumber, int pageNumber, string outputFolder)
    {
        try
        {
            if (imageData.TryGetPng(out var bytes))
            {
                await File.WriteAllBytesAsync(
                    GetFilepath(imageNumber, pageNumber, outputFolder, true, "png"),
                    bytes);
                
                return "png";
            }

            if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                await File.WriteAllBytesAsync(
                    GetFilepath(imageNumber, pageNumber, outputFolder, true, "bmp"),
                    bytesMemory.ToArray());
                
                return "bmp";
            }

            var bytesSpanAry = imageData.RawBytes.ToArray();
            if (bytesSpanAry.Length == 0)
            {
                throw new Exception("Cannot get bytes via either method");
            }

            await File.WriteAllBytesAsync(
                GetFilepath(imageNumber, pageNumber, outputFolder, true, "jpg"),
                bytesSpanAry);
            
            return "jpg";
        }
        catch (Exception ex)
        {
            if (ex is IOException)
            {
                return string.Empty;
            }

            // TODO should work out when need to deflate
            //bytesAry = Deflate(bytesAry);

            return string.Empty;
        }
    }

    public string GetFilepath(int imageNumber, int pageNumber, string outputFolder, bool createDirectory, string extension)
    {
        var outputFolderFull = $"{outputFolder}/PdfPig/Images";
        
        if (createDirectory)
        {
            Directory.CreateDirectory(outputFolderFull);
        }
        
        return $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.{extension}";
    }

    private static byte[] Deflate(byte[] input)
    {
        var cutInput = new byte[input.Length - 2];
        Array.Copy(input, 2, cutInput, 0, cutInput.Length);

        var stream = new MemoryStream();

        using (var compressStream = new MemoryStream(cutInput))
        using (var decompressor = new DeflateStream(compressStream, CompressionMode.Decompress))
            decompressor.CopyTo(stream);

        return stream.ToArray();
    }
}