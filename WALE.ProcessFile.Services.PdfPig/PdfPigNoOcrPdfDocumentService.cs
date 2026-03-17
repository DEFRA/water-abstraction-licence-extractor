using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.PdfPig.Models;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrPdfDocumentService : INoOcrPdfDocumentService
{
    public IInternalPdfDocument GetPdfDocument(IFileService fileService, string filename)
    {
        var fileStream = Task.Run(() => fileService.GetFileAsStreamAsync(filename)).Result;
        
        var document = PdfDocument.Open(
            fileStream,
            new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true,
                FilterProvider = ExpandedPdfPigFilterProvider.Instance,
            });

        document.AddSkiaPageFactory();
        return new PdfPigInternalPdfDocument(document);
    }

    public string? Name { get; set; } = GeneralConstants.PdfPigDataExtractorServiceName;
}