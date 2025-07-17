using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
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
                .Select(page => new PdfPage
                {
                    PdfPigPage = page,
                    Number = page.Number
                })
                .ToList();
            return _pages!;
        }
        set => _pages = value;
    }

    public MemoryStream GetPageAsPng(int pageNumber, IColor background)
    {
        if (FromCache)
        {
            throw new Exception("Cannot get image from cache");
        }
        
        return PdfPigDocument!.GetPageAsPng(
            pageNumber,
            background: background,
            scale: 3,
            quality: 100);
    }
    
    public void Dispose()
    {
        if (FromCache)
        {
            return;
        }
        
        PdfPigDocument!.Dispose();;
    }
}