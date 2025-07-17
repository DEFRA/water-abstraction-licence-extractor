namespace WALE.ProcessFile.Services.Models;

public class PdfPage
{
    public int PageNumber { get; set; }
    
    public string? ImageFilepath { get; set; }
    
    public IReadOnlyList<PdfPageProvider>? Providers { get; set; }
}