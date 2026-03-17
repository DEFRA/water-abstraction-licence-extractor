namespace WALE.ProcessFile.Core.Models;

public class SharePointFileIdInformation
{
    public Guid FileId { get; set; }
    
    public string? DmsFilePath { get; set; }

    public int ProcessRunId{ get; set; }
}