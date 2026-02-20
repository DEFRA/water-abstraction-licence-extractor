using UglyToad.PdfPig.Core;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrPageService(IInternalPdfDocumentPage page) : INoOcrPdfPageService
{
    public Task<IReadOnlyList<INoOcrPdfImageService>> GetImagesAsync()
    {
        ArgumentNullException.ThrowIfNull(page);

        try
        {
            var result = page
                .GetImages()
                .Select(image => new PdfPigNoOcrImageService(image))
                .ToList();

            return Task.FromResult((IReadOnlyList<INoOcrPdfImageService>)result);
        }
        catch (PdfDocumentFormatException exception)
        {
            Console.WriteLine($"ERROR (PdfPig) - PdfDocumentFormatException getting images - {exception.Message}");
            return Task.FromResult((IReadOnlyList<INoOcrPdfImageService>)[]);
        }
        catch (IndexOutOfRangeException exception)
        {
            Console.WriteLine($"ERROR (PdfPig) - IndexOutOfRangeException getting images - {exception.Message}");
            return Task.FromResult((IReadOnlyList<INoOcrPdfImageService>)[]);
        }
    }

    public int Number { get; set; } = page.Number;
}