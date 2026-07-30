namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemLinkedLicence
{
    public long LinkedLicenceId { get; set; }

    public long LicenceListItemId { get; set; }

    public string? LicenceNumber { get; set; }

    public string? RawScrapedLicenceNumber { get; set; }

    public string? DmsPermitNumber { get; set; }

    public Guid? DmsFileId { get; set; }

    public string? Filename { get; set; }

    public string? DmsPath { get; set; }

    public string? LicenceVersionId { get; set; }

    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; init; }

    public DateOnly? OriginalIssueDate { get; init; }
    public string? Issuer { get; set; }

    public string? NaldStatus { get; set; }

    public string? LicenceType { get; set; }

    public int? RegionId { get; set; }

    public string? SourceData { get; set; }
    
    
    public DateTime? NaldRevocationDate { get; init; }

    public DateTime? NaldExpiryDate { get; init; }

    public DateTime? NaldOrigEffectiveDate { get; init; }

    public DateTime? NaldOrigSignatureDate { get; init; }

    public DateTime? NaldSignatureDate { get; init; }

    public DateTime? NaldEffectiveStartDate { get; init; }

    public DateTime? NaldEffectiveEndDate { get; init; }

    public int? NaldIssueNumber { get; init; }

    public int? NaldIncrementNumber { get; init; }

    public string? NaldUpdateReason { get; init; }
    
    public string? DmsFileIdStatus { get; init; }

    public string? LicenceVersionNaldStatus { get; set; }
    
    public DateTime? DmsFileIdStatusDateUtc { get; init; }
    public List<LicenceListItemLinkLocation> Locations { get; set; } = [];
}