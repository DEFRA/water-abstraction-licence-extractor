using SkiaSharp;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class PdfDocument
{
    public bool FromCache { get; }
    public string PdfFilename { get; }
    
    public Guid FileId { get; set; }
    
    public string PdfFilenameNoExtension { get; }
    
    public IFileService FileService { get; }
    
    private IInternalPdfDocument? InternalDocument { get; set; }
    
    private IAlternativeImageProvider? AlternativeImageProvider { get; set; }
    
    private IOutputService OutputService { get; set; }
    
    INoOcrPdfDocumentService NoOcrPdfDocumentService { get; set; }
    
    INoOcrAlternativePdfDocumentService NoOcrAlternativePdfDocumentService { get; set; }
    
    int SkipFileIfMoreThenPages { get; set; }
    
    public PdfDocument(
        string pdfFilename,
        Guid fileId,
        bool fromCache,
        IOutputService outputService,
        INoOcrPdfDocumentService noOcrPdfDocumentService,
        INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService,
        LookupConfiguration configuration)
    {
        PdfFilename = pdfFilename;
        FileId = fileId;
        PdfFilenameNoExtension = FileHelper.GetFilenameWithoutExtension(pdfFilename)!;
        FileService = configuration.FileService;
        FromCache = fromCache;
        OutputService = outputService;
        NoOcrPdfDocumentService = noOcrPdfDocumentService;
        NoOcrAlternativePdfDocumentService = noOcrAlternativePdfDocumentService;
        SkipFileIfMoreThenPages = configuration.SkipFileWhenMoreThenPages;
            
        if (fromCache)
        {
            return;
        }
        
        OpenInternalDocument();
    }

    private void OpenInternalDocument()
    {
        if (InternalDocument != null)
        {
            return;
        }

        InternalDocument = NoOcrPdfDocumentService.GetPdfDocument(FileService, PdfFilename);
        if (InternalDocument.GetPages().Count > SkipFileIfMoreThenPages)
        {
            throw new TooManyPagesException(
                "Too many pages in this file - it is being skipped",
                InternalDocument.GetPages().Count);
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
                OpenInternalDocument();
            }
            
            _pages = InternalDocument!.GetPages()
                .Select(page =>
                {
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
            OpenInternalDocument();
        }

        var pdfPigBitmap = InternalDocument!.GetPageAsSKBitmap(
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