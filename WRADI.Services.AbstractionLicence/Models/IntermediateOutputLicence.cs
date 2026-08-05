using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models;

public class IntermediateOutputLicence
{
    public string? Filename;
    public string? LicenceHolder;
    public double? LicenceHolderOcrConfidence;
    public string? Ocr;
    public string?[]? Purposes;
    public string?[]? Points;
    public string? LicenceNumber;
    public double? LicenceNumberOcrConfidence;
    public int LimitsCount;
    public int AggregatesCount;
    public bool? NaldHasAggregateCondition;
    public string? IssueDate;
    public string? Issuer;
    public bool MeansFound;
    public string? Status;
    public LinkedLicence[]? LinkedLicences;
    public List<LicenceSet>? LicenceSets;
    public LicenceSetReference[] ? LicenceSetReferences;
    public Guid? DmsFileId;
}