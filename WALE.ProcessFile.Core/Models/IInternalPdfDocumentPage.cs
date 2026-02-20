namespace WALE.ProcessFile.Core.Models;

public interface IInternalPdfDocumentPage
{
    public int Number { get; set; }
    
    public int NumberOfImages { get; set; }

    public string? Text { get; set; }
    
    public object UnderlyingObject { get; set; }

    public List<IInternalPdfImage> GetImages();
}