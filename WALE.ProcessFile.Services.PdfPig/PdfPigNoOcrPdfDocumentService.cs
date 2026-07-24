using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.PdfPig.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrPdfDocumentService : INoOcrPdfDocumentService
{
    public async Task<IInternalPdfDocument?> GetPdfDocumentAsync(IFileService fileService, string filename)
    {
        var fileStream = await fileService.GetFileAsStreamAsync(filename);

        if (fileStream == null)
        {
            return null;
        }
        
        var sizeBytes = fileStream.Length;
        
        var document = PdfDocument.Open(
            fileStream,
            new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true,
                FilterProvider = ExpandedPdfPigFilterProvider.Instance,
            });

        document.AddSkiaPageFactory();
        
        var syncStream = Stream.Synchronized(fileStream);
        return new PdfPigInternalPdfDocument(document, syncStream, sizeBytes);
    }

    public string? Name { get; set; } = GeneralConstants.PdfPigDataExtractorServiceName;
}