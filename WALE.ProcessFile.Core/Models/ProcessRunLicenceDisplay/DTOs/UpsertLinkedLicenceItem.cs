using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class UpsertLinkedLicenceItem
{
    public string? LicenceNumber { get; init; }

    public string? RawScrapedLicenceNumber { get; init; }

    public string? DmsPermitNumber { get; init; }

    public string? DmsPath { get; init; }

    public Guid? DmsFileId { get; init; }

    public string? Filename { get; init; }

    public string LicenceVersionId { get; init; } =
        LicenceVersion.UnknownVersion;

    public DateOnly? EffectiveDate { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public DateOnly? IssueDate { get; init; }

    public DateOnly? OriginalIssueDate { get; init; }

    public string? Issuer { get; init; }

    public string? NaldStatus { get; init; }
    
    public string? LicenceVersionNaldStatus { get; init; }

    public string? LicenceType { get; init; }

    public int? RegionId { get; init; }

    public UpsertContainedInInformation[] ContainedIn { get; init; } = [];

    public string? ConditionData { get; init; }
    public DateTime? NaldRevocationDate { get; set; }
    
    public DateTime? NaldExpiryDate { get; set; }
    
    public DateTime? NaldOrigEffectiveDate { get; set; }
    
    public DateTime? NaldOrigSignatureDate { get; set; }
    public DateTime? NaldSignatureDate { get; set; }
    public DateTime? NaldEffectiveStartDate { get; set; }
    public DateTime? NaldEffectiveEndDate { get; set; }
    
    public int? NaldIssueNumber { get; set; }
    public int? NaldIncrementNumber { get; set; }
    public string? NaldUpdateReason { get; set; }
    
    public string? DmsFileIdStatus { get; set; }
    
    public DateTime? DmsFileIdStatusDateUtc { get; set; }
    public string? SourceData { get; init; }
}