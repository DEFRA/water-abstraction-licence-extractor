namespace WALE.ProcessFile.Core.Models;

public class NaldLicenceVersionDataLine
{
    public string LookupKey => AablId.ToString()!;
    public int? AablId { get; set; }
    public short IssueNo { get; set; }
    public short IncrNo { get; set; }
    public string? AabvType { get; set; }
    public DateTime? EffStDate { get; set; }
    public string? Status { get; set; }
    public string? AsrcCode { get; set; }
    public DateTime? LicSigDate { get; set; }
    public string? AppNo { get; set; }
    public DateTime? EffEndDate { get; set; }
    public string? WaAltyCode { get; set; }
    public short FgacRegionCode { get; set; }
}