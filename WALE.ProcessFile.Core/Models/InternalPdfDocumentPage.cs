namespace WALE.ProcessFile.Core.Models;

public class InternalPdfDocumentPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }
    public string? Text { get; set; }
}