using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Rendering.Skia;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Models;

public class PdfDocument
{
    public bool FromCache { get; }
    public string PdfFilePath { get; }
    
    private UglyToad.PdfPig.PdfDocument? PdfPigDocument { get; set; }
    
    public PdfDocument(string pdfFilePath, bool fromCache)
    {
        PdfFilePath = pdfFilePath;
        FromCache = fromCache;
        
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
                    var pdfPage = new PdfPage
                    {
                        PdfPigPage = page,
                        Number = page.Number,
                        NumberOfImages = page.NumberOfImages,
                        Text = page.Text
                    };

                    //var OutputFolder = ""; // TODO
                    //dfPage.ImageFilepath = $"{OutputFolder}/{pdfPage.GetImageFilepath("PdfPig")}";
                    
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
        if (FromCache && PdfPigDocument == null)
        {
            OpenPdfPigDocument();
        }
        
        return PdfPigDocument!.GetPageAsSKBitmap(
            pageNumber,
            background: background,
            scale: 2F);
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