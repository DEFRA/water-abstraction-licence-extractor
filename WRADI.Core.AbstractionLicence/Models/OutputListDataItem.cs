namespace WRADI.Core.AbstractionLicence.Models;

public class OutputListDataItem
{
    public int? processRunId { get; set; }
    
    public Guid fileId { get; set; }

    public string? filename { get; set; }
    
    public string? licenceNumber { get; set; }
    
    public string? licenceHolder { get; set; }
    
    public string?[]? purposes { get; set; }
    
    public string?[]? points { get; set; }
    
    public int limitsCount { get; set; }

    public string[]? aggregateIds { get; set; }

    public int aggregatesCount => aggregateIds?.Length ?? 0;

    public bool? naldHasAggregateCondition { get; set; }
    
    public bool ocr { get; set; }
    
    public string? issueDate { get; set; }
    
    public string? issuer { get; set; }
    
    public bool meansFound { get; set; }
    
    public string? status{ get; set; }
    
    public LinkedLicence[]? linkedLicences { get; set; }
    
    public OutputListDataItemLicenceSet?[]? licenceSets { get; set; }
    
    public LicenceSectionVerificationSummary[]? licenceSectionVerifications { get; set; }
}