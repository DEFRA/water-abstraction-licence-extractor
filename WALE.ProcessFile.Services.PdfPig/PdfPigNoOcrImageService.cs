using Tesseract;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrImageService(IInternalPdfImage imageData) : INoOcrPdfImageService
{
    public async Task<(string Extension, int ImageNumber)> SaveImageBytesAsync(
        Guid fileId,
        int imageNumber,
        int pageNumber,
        ICacheService cacheService,
        int processRunId)
    {
        const string pngExtension = "png";
        const string bmpExtension = "bmp";
        const string jpgExtension = "jpg";

        string returnExtension;
        byte[]? bytes;

        Pix? pix;
        const string deflateNeededErrorText = "Failed to load image from memory.";
        
        try
        {
            if (imageData.TryGetPng(out bytes))
            {
                returnExtension = pngExtension;
                
                try
                {
                    ConsoleHelper.WriteToBuffer = true;
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.TryRemoveLastLine();
                    
                    if (!ex.Message.Contains(deflateNeededErrorText))
                    {
                        throw;
                    }

                    ConsoleHelper.WriteLine($"INFO - {nameof(PdfPigNoOcrImageService)} - Trying deflate");
                    
                    returnExtension = jpgExtension;
                    bytes = ImageHelper.Deflate(bytes!);
                    pix = Pix.LoadFromMemory(bytes);
                }
                finally
                {
                    ConsoleHelper.WriteToBuffer = false;                    
                }
            }
            else if (imageData.TryGetBytesAsMemory(out var bytesMemory))
            {
                returnExtension = bmpExtension;
                bytes = bytesMemory.ToArray();

                try
                {
                    ConsoleHelper.WriteToBuffer = true;
                    pix = Pix.LoadFromMemory(bytes);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.TryRemoveLastLine();
                    
                    if (!ex.Message.Contains(deflateNeededErrorText))
                    {
                        throw;
                    }
                    
                    ConsoleHelper.WriteLine($"INFO - {nameof(PdfPigNoOcrImageService)} - Trying deflate");

                    returnExtension = jpgExtension;
                    bytes = ImageHelper.Deflate(bytes);
                    pix = Pix.LoadFromMemory(bytes);
                }
                finally
                {
                    ConsoleHelper.WriteToBuffer = false;                    
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
                    ConsoleHelper.TryRemoveLastLine();

                    if (!ex.Message.Contains(deflateNeededErrorText))
                    {
                        throw;
                    }
                    
                    ConsoleHelper.WriteLine($"INFO - {nameof(PdfPigNoOcrImageService)} - Trying deflate");

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
            ConsoleHelper.WriteLine($"ERROR (IMPORTANT) - {nameof(PdfPigNoOcrImageService)} - SaveImageBytesAsync, {exception} - {fileId}");

            // Write an empty entry into the table
            await cacheService.SaveImageOnPageAsync(
                [],
                -1,
                -1,
                fileId,
                GeneralConstants.PdfPigDataExtractorServiceName,
                imageNumber,
                pageNumber,
                "error",
                processRunId);

            return ("error", imageNumber);
        }
        
        var size = await cacheService.SaveImageOnPageAsync(
            bytes!,
            pix.Width,
            pix.Height,
            fileId,
            GeneralConstants.PdfPigDataExtractorServiceName,
            imageNumber,
            pageNumber,
            returnExtension,
            processRunId);
        
        var roundedSizeKb = (size / 1024.0).ToString("0.0");
        ConsoleHelper.WriteLine($"INFO - PdfPigNoOcrImageService - Saved page image P{pageNumber} I{imageNumber} ({roundedSizeKb}kb) - {fileId}");
        
        return (returnExtension, imageNumber);
    }
}