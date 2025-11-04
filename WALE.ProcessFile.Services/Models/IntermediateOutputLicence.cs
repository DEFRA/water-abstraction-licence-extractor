using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Models;

public class IntermediateOutputLicence
{
    public int LineNumber { get; set; }
    public int StartNumber { get; set; }
    public string? Filename { get; set; }
    public string? LicenceHolder { get; set; }
    public double? LicenceHolderOcrConfidence { get; set; }
    public string? Ocr { get; set; }
    public string?[]? Purposes { get; set; }
    public string?[]? Points { get; set; }
    public string? ServiceName { get; set; }
    public int Certainty { get; set; }
    public string? MatchType { get; set; }
    public int Duration { get; set; }
    public string? MatchedLabelText { get; set; }
    public string? MatchedLabelPosition { get; set; }
    public string? LicenceNumber { get; set; }
    public double? LicenceNumberOcrConfidence { get; set; }
    public int LimitsCount { get; set; }
    public int AggregatesCount { get; set; }
    public string? IssueDate { get; set; }
    public string? Issuer { get; set; }
    public bool MeansFound { get; set; }
    public LinkedLicence[]? LinkedLicences { get; set; }
    public List<LicenceSet>? LicenceSets { get; set; }
    public LicenceSetReference[] ? LicenceSetReferences { get; set; }
    public int NodeId { get; set; }
}