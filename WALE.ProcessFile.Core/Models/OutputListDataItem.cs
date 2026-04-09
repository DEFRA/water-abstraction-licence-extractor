using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class OutputListDataItem
{
    public string? imagePath { get; set; }
    public string? filename{ get; set; }
    public string? licenceNumber{ get; set; }
    public string? licenceHolder{ get; set; }
    public string?[]? purposes { get; set; }
    public string?[]? points { get; set; }
    public int limitsCount { get; set; }
    public int aggregatesCount { get; set; }
    public bool ocr { get; set; }
    public string? issueDate { get; set; }
    public string? issuer { get; set; }
    public bool meansFound { get; set; }
    public string? status{ get; set; }
    public LinkedLicence[]? linkedLicences { get; set; }
    public OutputListDataItemLicenceSet?[]? licenceSets { get; set; }
    public List<LicenceVerificationSummary>? licenceVerificationSummary { get; set; }
}

public class LicenceVerificationSummary
{
    public Guid LicenceFileId { get; set; }
    public string? LicenceSectionName { get; set; }
    public string? VerificationType { get; set; }
}