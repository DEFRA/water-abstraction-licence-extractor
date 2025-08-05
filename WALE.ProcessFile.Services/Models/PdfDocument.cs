using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Models;

public class PdfDocument
{
    public bool FromCache { get; set; }
    public string PdfFilePath { get; set; }
    public string OutputFolder { get; set; }    
    
    private UglyToad.PdfPig.PdfDocument? PdfPigDocument { get; set; }
    
    public PdfDocument(string pdfFilePath, string outputFolder, bool fromCache)
    {
        PdfFilePath = pdfFilePath;
        OutputFolder = outputFolder;
        FromCache = fromCache;

        if (fromCache)
        {
            return;
        }
        
        PdfPigDocument = UglyToad.PdfPig.PdfDocument.Open(
            pdfFilePath,
            new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true,
                FilterProvider = ExpandedPdfPigFilterProvider.Instance
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
            
            if (FromCache)
            {
                return [];
            }
            
            _pages = PdfPigDocument!.GetPages()
                .Select(page =>
                {
                    var pdfPage = new PdfPage
                    {
                        PdfPigPage = page,
                        Number = page.Number,
                        NumberOfImages = page.NumberOfImages,
                        Text = page.Text
                    };
                    pdfPage.ImageFilepath = $"{OutputFolder}/{pdfPage.GetImageFilepath("PdfPig")}";
                    
                    pdfPage.Providers.Add(new PdfPageProvider
                    {
                        Provider = "PdfPig",
                        Text = [page.Text]
                    });
                    
                    return pdfPage;
                })
                .ToList();
            return _pages!;
        }
        set => _pages = value;
    }

    public SKBitmap GetPageAsSkBitmap(int pageNumber, IColor background)
    {
        if (FromCache)
        {
            throw new Exception("Cannot get image from cache");
        }
        
        return PdfPigDocument!.GetPageAsSKBitmap(
            pageNumber,
            background: background,
            scale: 2F);
    }
    
    public void Dispose()
    {
        if (FromCache)
        {
            return;
        }
        
        PdfPigDocument!.Dispose();
    }
}