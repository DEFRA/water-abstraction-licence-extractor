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

        if (fromCache) return;
        
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

    private IReadOnlyList<Page>? _pages;
    
    public IReadOnlyList<Page> Pages
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
            
            _pages = PdfPigDocument!.GetPages().ToList();
            return _pages!;
        }
    }

    public MemoryStream GetPageAsPng(int pageNumber, IColor background)
    {
        if (FromCache)
        {
            throw new Exception("Cannot get image from cache");
        }
        
        return PdfPigDocument!.GetPageAsPng(pageNumber, background: background);
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