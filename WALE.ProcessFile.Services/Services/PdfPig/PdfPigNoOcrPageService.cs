using UglyToad.PdfPig.Content;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrPageService(Page page) : INoOcrPdfPageService
{
    public async Task<IEnumerable<INoOcrPdfImageService>> GetImagesAsync()
    {
        return await Task.Run(() =>
        {
            return page
                .GetImages()
                .Select(image => new PdfPigNoOcrImageService(image))
                .ToList();
        });
    }

    public int Number { get; set; } = page.Number;
}