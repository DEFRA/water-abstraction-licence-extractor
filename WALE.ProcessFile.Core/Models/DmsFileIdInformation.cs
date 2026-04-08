namespace WALE.ProcessFile.Core.Models;

public class DmsFileIdInformation
{
    public Guid FileId { get; set; }
    
    public string? DmsFilePath { get; set; }

    public int ProcessRunId { get; set; }

    public string? Status { get; set; }
    
    public DateTime StatusDateUtc { get; set; }
}