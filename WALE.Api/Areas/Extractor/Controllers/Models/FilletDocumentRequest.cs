namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class FilletDocumentRequest
{
    public string? pdfFileName { get; set; }
        
    public Guid fileId { get; set; }

    public int regionId { get; set; }
    
    public int processRunId { get; set; }
    
    public int skipFileWhenMoreThenPages { get; set; }
}