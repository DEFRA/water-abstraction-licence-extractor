using CsvHelper.Configuration.Attributes;

namespace WALE.ProcessFile.Core.Models;

public class NaldGeneralDataLine
{
    [Index(0)]
    public string? Region { get; set; }

    [Index(4)]
    public string? LicenceNo { get; set; }

    [Index(6)]
    public string? ExpiryDate { get; set; }

    [Index(7)]
    public string? VersionStartDate { get; set; }

    [Index(44)]
    public string? PointName { get; set; }

    [Index(46)]
    public long PointId { get; set; }

    [Index(47)]
    public string? Ngr1 { get; set; }

    [Index(51)]
    public string? Ngr1Cartesian { get; set; }

    [Index(55)]
    public double? LicenceWideAnnualQty { get; set; }

    [Index(56)]
    public double? LicenceWideDailyQty { get; set; }

    [Index(57)]
    public double? LicenceWideHourlyQty { get; set; }

    [Index(58)]
    public double? LicenceWideInstQty { get; set; }

    [Index(59)]
    public long? ConditionId { get; set; }

    [Index(60)]
    public string? Condition { get; set; }
}