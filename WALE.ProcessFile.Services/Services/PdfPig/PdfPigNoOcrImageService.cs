using System.IO.Compression;
using Tesseract;
using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrImageService(IPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<string?> SaveImageBytesAsync(string folderPath, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId)
    {
        const string pngExtension = "png";
        const string bmpExtension = "bmp";
        const string jpgExtension = "jpg";

        string returnExtension;
        byte[]? bytes;

        Pix? pix;
        
        try
        {
            if (imageData.TryGetPng(out bytes))
            {
                returnExtension = pngExtension;
                
                try
                {
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Failed to load image from memory."))
                    {
                        throw;
                    }

                    returnExtension = jpgExtension;
                    bytes = ImageHelper.Deflate(bytes);
                    pix = Pix.LoadFromMemory(bytes);
                }
            }
            else if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                returnExtension = bmpExtension;
                bytes = bytesMemory.ToArray();

                try
                {
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Failed to load image from memory."))
                    {
                        throw;
                    }

                    returnExtension = jpgExtension;
                    bytes = ImageHelper.Deflate(bytes);
                    pix = Pix.LoadFromMemory(bytes);
                }
            }
            else
            {
                var bytesSpanAry = imageData.RawBytes.ToArray();
                if (bytesSpanAry.Length == 0)
                {
                    throw new Exception("Cannot get bytes via either method");
                }

                bytes = bytesSpanAry;
                returnExtension = jpgExtension;

                try
                {
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("Failed to load image from memory."))
                    {
                        throw;
                    }

                    bytes = ImageHelper.Deflate(bytes);
                    pix = Pix.LoadFromMemory(bytes);
                }
            }
        }
        catch (Exception exception)
        {
            // TODO - throw?
            Console.WriteLine("ERROR - " + exception);
            return null;
        }
        
        await cacheService.SaveImageOnPageAsync(
            bytes,
            pix.Width,
            pix.Height,
            folderPath,
            PdfDataExtractorService.Name,
            imageNumber,
            pageNumber,
            returnExtension,
            processRunId);
        
        return returnExtension;
    }
}