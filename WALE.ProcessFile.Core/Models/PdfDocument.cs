using SkiaSharp;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class PdfDocument(
    string pdfFilename,
    Guid fileId,
    bool fromCache,
    long sizeBytes,
    IOutputService outputService,
    INoOcrPdfDocumentService noOcrPdfDocumentService,
    INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService,
    LookupConfiguration configuration)
{
    public bool FromCache { get; } = fromCache;
    public long SizeBytes { get; set; } = sizeBytes;
    public string PdfFilename { get; } = pdfFilename;

    public Guid FileId { get; set; } = fileId;

    private IFileService FileService { get; } = configuration.FileService;

    private IInternalPdfDocument? InternalDocument { get; set; }
    
    private IAlternativeImageProvider? AlternativeImageProvider { get; set; }
    
    private IOutputService OutputService { get; set; } = outputService;

    INoOcrPdfDocumentService NoOcrPdfDocumentService { get; set; } = noOcrPdfDocumentService;

    INoOcrAlternativePdfDocumentService NoOcrAlternativePdfDocumentService { get; set; } = noOcrAlternativePdfDocumentService;

    int SkipFileIfMoreThenPages { get; set; } = configuration.SkipFileWhenMoreThenPages;

    public static int SkipFileIfMoreThenImages { get; set; } = 50;

    public async Task OpenInternalDocumentAsync()
    {
        if (InternalDocument != null)
        {
            return;
        }

        InternalDocument = await NoOcrPdfDocumentService.GetPdfDocumentAsync(FileService, PdfFilename);
        SizeBytes = InternalDocument.SizeBytes;
        
        if (Pages.Count > SkipFileIfMoreThenPages)
        {
            throw new TooManyPagesException(
                "Too many pages in this file - it is being skipped",
                Pages.Count);
        }
        
        AlternativeImageProvider = NoOcrAlternativePdfDocumentService.GetAlternativeImageProvider();
    }

    private IReadOnlyList<PdfPage>? _pages;
    
    public IReadOnlyList<PdfPage> Pages
    {
        get
        {
            if (_pages != null)
            {
                return _pages;
            }
            
            if (FromCache && InternalDocument == null)
            {
                throw new Exception("PdfDocument not initialized correctly");
            }
            
            _pages = InternalDocument!.GetPages()
                .Select(page =>
                {
                    if (page.NumberOfImages > SkipFileIfMoreThenImages)
                    {
                        throw new TooManyImagesException(
                            "Too many images on this page (in this file) - it is being skipped",
                            page.NumberOfImages,
                            page.NumberOfImages);
                    }
                    
                    var screenshotPaths = OutputService.GetPageScreenshotReferences(
                        page.Number,
                        "PdfPig",
                        FileId);
                    
                    var pdfPage = new PdfPage
                    {
                        InternalPage = page,
                        Number = page.Number,
                        NumberOfImages = page.NumberOfImages,
                        DigitalText = page.Text,
                        ScreenshotFilepaths = screenshotPaths
                            .Select(sp => sp.ImageReference)
                            .ToList()!
                    };

                    foreach (var (providerName, _) in screenshotPaths)
                    {
                        pdfPage.Providers.Add(new PdfPageProvider
                        {
                            Provider = providerName,
                            Text = [page.Text!]
                        });
                    }
                    
                    return pdfPage;
                })
                .ToList();
            
            return _pages!;
        }
        set => _pages = value;
    }

    public async Task<List<(string Provider, SKBitmap Bitmap)>> GetPageAsSkBitmapAsync(int pageNumber, string noOcrServiceName)
    {
        if (FromCache && InternalDocument == null)
        {
            await OpenInternalDocumentAsync();
        }

        var pdfPigBitmap = InternalDocument!.GetPageAsSkBitmap(
            pageNumber,
            3F);

        var docnetBitmap = await AlternativeImageProvider!.GetPageAsSkBitmapAsync(
            FileService,
            PdfFilename,
            1080,
            1920,
            pageNumber);

        return
        [
            (noOcrServiceName, pdfPigBitmap),
            (GeneralConstants.DocnetExtractorServiceName, docnetBitmap)
        ];
    }

    public List<DocumentLine>? DocumentLines { get; set; }
    
    public ImageMetadata? ImagesMetadata { get; set; }

    public void Dispose()
    {
        if (FromCache && InternalDocument == null)
        {
            return;
        }
        
        InternalDocument!.Dispose();
    }
}