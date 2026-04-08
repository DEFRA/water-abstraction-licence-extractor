namespace WALE.Tools.Models;

public class LinkedLicencesCsvLine
{
    public string? Filename { get; set; }
    
    public Guid? FileId { get; set; }
    
    public string? FileIdStatus { get; set; }
    
    public string? FileIdStatusChangeDate { get; set; }
    
    public string? DmsPath { get; set; }
    
    public int? IssueNumber { get; set; }
    
    public int? IncrementNumber { get; set; }
    
    public string? LicenceNumber { get; set; }
    
    public string? PermitNumber { get; set; }
    
    public string? ScrapedLicenceNumber { get; set; }
    
    public string? ArepEuicCode { get; set; }
    
    public string? IssuedBy { get; set; }
    
    public string? DateOfIssue { get; set; }
    
    public bool? IsLive { get; set; }
    
    public bool? IsLapsed { get; set; }
    
    public bool? IsExpired { get; set; }

    public bool? IsRevoked { get; set; }    
    
    public bool? IsImpoundment { get; set; }
    
    public bool LicenceFoundInList { get; set; }
    
    public bool HasInlicenceAggregates { get; set; }
    
    public bool HasLicenceToLicenceAggregates { get; set; }

    public string? LinkedLicenceNumber { get; set; }
    
    public string? ScrapedLinkedLicenceNumber { get; set; }
    
    public string? LinkedLicenceFilename { get; set; }
    
    public string? LinkedLicenceDmsPath { get; set; }
    
    public string? LinkedLicenceDocumentOutgoing { get; set; }
    
    public string? LinkedLicenceNaldOutgoing { get; set; }
    
    public string? LinkedLicenceDocumentIncoming { get; set; }
    
    public string? LinkedLicenceNaldIncoming { get; set; }
    
    public bool? LinkedLicenceIsLive { get; set; }
    
    public bool? LinkedLicenceIsLapsed { get; set; }
    
    public bool? LinkedLicenceIsExpired { get; set; }
    
    public bool? LinkedLicenceIsRevoked { get; set; }
    
    public bool? LinkedLicenceIsImpoundment { get; set; }
    
    public bool? LinkedLicenceFoundInList { get; set; }

    public LinkedLicencesCsvLine Clone()
    {
        return (LinkedLicencesCsvLine)MemberwiseClone();
    }
}