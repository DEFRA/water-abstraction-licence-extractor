namespace WALE.ProcessFile.Services.Models;

public class OutputLine
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
    public string? LinkedLicenceNumbers;
    public string? LicenceSetIds;
    public string? ShortLicenceSetIds;
    public int NodeId;
}