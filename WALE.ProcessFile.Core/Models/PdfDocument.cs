using Docnet.Core.Models;
using SkiaSharp;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Docnet;

namespace WALE.ProcessFile.Core.Models;

public class PdfDocument
{
    public bool FromCache { get; }
    public string PdfFilename { get; }
    
    public string PdfFilenameNoExtension { get; }
    
    public string PdfFolder { get; }
    
    private IInternalPdfDocument? InternalDocument { get; set; }
    
    private IOutputService OutputService { get; set; }
    
    INoOcrPdfDocumentService NoOcrPdfDocumentService { get; set; }
    
    public PdfDocument(
        string pdfFilename,
        bool fromCache,
        IOutputService outputService,
        INoOcrPdfDocumentService noOcrPdfDocumentService,
        LookupConfiguration configuration)
    {
        PdfFilename = pdfFilename;
        PdfFilenameNoExtension = FileHelper.GetFilenameWithoutExtension(pdfFilename)!;
        PdfFolder = configuration.PdfFolder;
        FromCache = fromCache;
        OutputService = outputService;
        NoOcrPdfDocumentService = noOcrPdfDocumentService;
        
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

        InternalDocument = NoOcrPdfDocumentService.GetPdfDocument($"{PdfFolder}{PdfFilename}");
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
                        PdfFilename);
                    
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

    public List<(string Provider, SKBitmap Bitmap)> GetPageAsSkBitmap(int pageNumber, string noOcrServiceName)
    {
        if (FromCache && InternalDocument == null)
        {
            OpenInternalDocument();
        }

        var pdfPigBitmap = InternalDocument!.GetPageAsSKBitmap(
            pageNumber,
            3F);

        var docnetBitmap = new DocnetBitmap().GetPageAsSkBitmap(
            "{PdfFolder}{PdfFilename}",
            new PageDimensions(1080, 1920),
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