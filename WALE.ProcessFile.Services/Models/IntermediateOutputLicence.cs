using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Models;

public class IntermediateOutputLicence
{
    public int LineNumber;
    public int StartNumber;
    public string? Filename;
    public string? LicenceHolder;
    public double? LicenceHolderOcrConfidence;
    public string? Ocr;
    public string?[]? Purposes;
    public string?[]? Points;
    public string? ServiceName;
    public int Certainty;
    public string? MatchType;
    public int Duration;
    public string? MatchedLabelText;
    public string? MatchedLabelPosition;
    public string? LicenceNumber;
    public double? LicenceNumberOcrConfidence;
    public int LimitsCount;
    public int AggregatesCount;
    public string? IssueDate;
    public string? Issuer;
    public bool MeansFound;
    public string? Status;
    public LinkedLicence[]? LinkedLicences;
    public List<LicenceSet>? LicenceSets;
    public LicenceSetReference[] ? LicenceSetReferences;
    public Guid? DmsFileId;
    public int NodeId;
}