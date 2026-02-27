using Tesseract;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrImageService(IInternalPdfImage imageData) : INoOcrPdfImageService
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
                    bytes = ImageHelper.Deflate(bytes!);
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
                    ConsoleHelper.WriteToBuffer = true;
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.RemoveLastLine();

                    if (!ex.Message.Contains("Failed to load image from memory."))
                    {
                        Console.WriteLine($"ERROR - {nameof(PdfPigNoOcrImageService)} - {ex}");
                        throw;
                    }

                    bytes = ImageHelper.Deflate(bytes);
                    pix = Pix.LoadFromMemory(bytes);
                }
                finally
                {
                    ConsoleHelper.WriteToBuffer = false;                    
                }
            }
        }
        catch (Exception exception)
        {
            // TODO - throw?
            ConsoleHelper.WriteLine("ERROR - " + exception);
            return null;
        }
        
        await cacheService.SaveImageOnPageAsync(
            bytes!,
            pix.Width,
            pix.Height,
            folderPath,
            GeneralConstants.PdfPigDataExtractorServiceName,
            imageNumber,
            pageNumber,
            returnExtension,
            processRunId);
        
        return returnExtension;
    }
}