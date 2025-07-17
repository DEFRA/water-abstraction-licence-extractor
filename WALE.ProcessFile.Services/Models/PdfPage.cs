namespace WALE.ProcessFile.Services.Models;

public class PdfPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }

    public string? ImageFilepath => $"/PdfPig/Images/page-{Number}.png";

    public IReadOnlyList<PdfPageProvider>? Providers { get; set; }
    
    public UglyToad.PdfPig.Content.Page? PdfPigPage { get; set; }
}