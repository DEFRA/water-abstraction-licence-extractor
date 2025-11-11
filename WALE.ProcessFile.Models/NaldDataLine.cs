using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Models;

public class NaldDataLine
{
    [Index(0)]
    public string? Region { get; set; }

    [Index(4)]
    public string? LicenceNo { get; set; }

    [Index(6)]
    public string? ExpiryDate { get; set; }

    [Index(7)]
    public string? VersionStartDate { get; set; }

    [Index(60)]
    public string? Condition { get; set; }
}