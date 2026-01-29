namespace WALE.ProcessFile.Core.Models;

public class NaldDataPurpose
{
    public int? Id { get; set; }
    
    public long PurposeId { get; init; }
    
    public string? PurposeCode { get; init; }
    
    public string? PurposeUseCode { get; init; }
    
    public string? PurposeUseDescription { get; init; }

    public override string ToString()
    {
        return $"{Id}{PurposeId}{PurposeCode}{PurposeUseCode}{PurposeUseDescription}";
    }
}