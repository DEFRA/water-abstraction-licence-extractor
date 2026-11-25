namespace WRADI.Core.AbstractionLicence.Models;

public class NaldImpoundmentLicenceVersionDataLine
{
    public string LookupKey => $"{FgacRegionCode}|{AimpId}";
    public int? AimpId { get; set; }
    public short IssueNo { get; set; }
    public short IncrNo { get; set; }
    public string? AimvType { get; set; }
    public DateTime? EffStDate { get; set; }
    public string? Status { get; set; }
    public string? AsrcCode { get; set; }
    public DateTime? LicSigDate { get; set; }
    public string? AppNo { get; set; }
    public DateTime? EffEndDate { get; set; }
    public short FgacRegionCode { get; set; }
}