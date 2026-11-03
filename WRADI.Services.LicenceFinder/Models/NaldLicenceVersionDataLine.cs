using System;

namespace WRADI.Services.LicenceFinder.Models;

public class NaldLicenceVersionDataLine
{
    public string LookupKey => $"{FgacRegionCode}|{AablId}";
    public int? AablId { get; set; }
    public short IssueNo { get; set; }
    public short? IncrNo { get; set; }
    public string? AabvType { get; set; }
    public DateTime? LicSigDate { get; set; }
    public short FgacRegionCode { get; set; }
}