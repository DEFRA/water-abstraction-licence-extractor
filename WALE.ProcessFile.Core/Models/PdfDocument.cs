using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.Services.PdfPig;

namespace WALE.ProcessFile.Core.Models;

public class PdfDocument
{
    public bool FromCache { get; }
    public string PdfFilePath { get; }
    
    private UglyToad.PdfPig.PdfDocument? PdfPigDocument { get; set; }
    
    private IOutputService OutputService { get; set; }
    
    private static readonly DocLib DocLibInstance = DocLib.Instance;
    
    public PdfDocument(string pdfFilePath, bool fromCache, IOutputService outputService)
    {
        PdfFilePath = pdfFilePath;
        FromCache = fromCache;
        OutputService = outputService;
        
        if (fromCache)
        {
            return;
        }
        
        OpenPdfPigDocument();
    }

    private void OpenPdfPigDocument()
    {
        if (PdfPigDocument != null)
        {
            return;
        }
        
        PdfPigDocument = UglyToad.PdfPig.PdfDocument.Open(
            PdfFilePath,
            new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true,
                FilterProvider = ExpandedPdfPigFilterProvider.Instance,
            });

        PdfPigDocument!.AddSkiaPageFactory();
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
            
            if (FromCache && PdfPigDocument == null)
            {
                OpenPdfPigDocument();
            }
            
            _pages = PdfPigDocument!.GetPages()
                .Select(page =>
                {
                    var screenshotPaths = OutputService.GetPageScreenshotReferences(
                        page.Number,
                        "PdfPig",
                        PdfFilePath);
                    
                    var pdfPage = new PdfPage
                    {
                        PdfPigPage = page,
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
                            Text = [page.Text]
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
        if (FromCache && PdfPigDocument == null)
        {
            OpenPdfPigDocument();
        }

        var pdfPigBitmap = PdfPigDocument!.GetPageAsSKBitmap(
            pageNumber,
            3F);

        using var docReader = DocLibInstance.GetDocReader(
            PdfFilePath,
            new PageDimensions(1080, 1920));

        using var pageReader = docReader.GetPageReader(pageNumber - 1);
        var rawBytes = pageReader.GetImage();

        for (var i = 0; i < rawBytes.Length / 4; i++)
        {
            var j = i * 4;
            var alpha = rawBytes[j];
            var red = rawBytes[j + 1];
            var green = rawBytes[j + 2];
            var blue = rawBytes[j + 3];

            if (alpha != 0 || red != 0 || green != 0 || blue != 0) continue;

            rawBytes[j] = byte.MaxValue;
            rawBytes[j + 1] = byte.MaxValue;
            rawBytes[j + 2] = byte.MaxValue;
            rawBytes[j + 3] = byte.MaxValue;
        }

        var skImage = SKImage.FromPixelCopy(
            new SKImageInfo(
                pageReader.GetPageWidth(),
                pageReader.GetPageHeight(),
                SKColorType.Bgra8888
            ),
            rawBytes);

        var docnetBitmap = SKBitmap.FromImage(skImage);

        return
        [
            (noOcrServiceName, pdfPigBitmap),
            ("Docnet", docnetBitmap)
        ];
    }

    public void Dispose()
    {
        if (FromCache && PdfPigDocument == null)
        {
            return;
        }
        
        PdfPigDocument!.Dispose();
    }
}