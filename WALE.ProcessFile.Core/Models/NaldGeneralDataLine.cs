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

    [Index(26)]
    public long PurposeId { get; set; }
    
    [Index(27)]
    public string? PurposeCode { get; set; }

    [Index(33)]
    public string? PrimaryPointType { get; set; }
    
    [Index(36)]
    public string? SecondaryPointType { get; set; }
    
    [Index(39)]
    public string? PurposeUseCode { get; set; }
    
    [Index(40)]
    public string? PurposeUseDescription { get; set; }
    
    [Index(41)]
    public string? PeriodStart { get; set; }
    
    [Index(42)]
    public string? PeriodEnd { get; set; }
    
    [Index(44)]
    public string? PointName { get; set; }
    
    [Index(45)]
    public string? PointCategory { get; set; }

    [Index(46)]
    public long PointId { get; set; }

    [Index(47)]
    public string? Ngr1 { get; set; }
    
    [Index(48)]
    public string? Ngr2 { get; set; }
    
    [Index(49)]
    public string? Ngr3 { get; set; }
    
    [Index(50)]
    public string? Ngr4 { get; set; }

    [Index(51)]
    public string? Ngr1Cartesian { get; set; }
    
    [Index(52)]
    public string? Ngr2Cartesian { get; set; }
    
    [Index(53)]
    public string? Ngr3Cartesian { get; set; }
    
    [Index(54)]
    public string? Ngr4Cartesian { get; set; }

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