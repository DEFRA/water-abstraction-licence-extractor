using System.IO.Compression;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<byte[]> GetImageBytesAsync(int imageNumber, int pageNumber, string outputFolder)
    {
        var outputFolderFull = $"{outputFolder}/PdfPig/Images";
        Directory.CreateDirectory(outputFolderFull);
            
        var outputFilename = $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.jpg";

        // TODO
        /*
        if (File.Exists(outputFilename))
        {
            return null;
        }*/
        
        return await Task.Run(() =>
        {
            byte[]? bytesSpanAry = null;
            ReadOnlyMemory<byte> bytesMemory = default;
            
            if (!imageData.TryGetPng(out var bytes) && !imageData.TryGetBytesAsMemory(out bytesMemory))
            {
                bytesSpanAry = imageData.RawBytes.ToArray();
                if (bytesSpanAry.Length == 0)
                {
                    throw new Exception("Cannot get bytes via either method");
                }
            }
            
            var bytesAry = bytes?.Length > 0
                ? bytes
                : bytesMemory.Length > 0
                    ? bytesMemory.ToArray()
                    : bytesSpanAry!;

            try
            {
                using var image = Image.Load(new DecoderOptions(), bytesAry);
                image.SaveAsJpeg(outputFilename);
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                {
                    return bytesAry;
                }
                
                // TODO should check what exception it is before trying this
                bytesAry = Deflate(bytesAry);

                using var image = Image.Load(new DecoderOptions(), bytesAry);
                image.SaveAsJpeg(outputFilename);                
            }

            return bytesAry;
        });
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