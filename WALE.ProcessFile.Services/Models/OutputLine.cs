namespace WALE.ProcessFile.Services.Models;

public class OutputLine
{
    public int LineNumber;
    public int StartNumber;
    public string? Filename;
    public string? LicenceHolder;
    public double? LicenceHolderOcrConfidence;
    public string? Ocr;
    public string? Purposes;
    public string? Points;
    public string? ServiceName;
    public int Certainty;
    public string? MatchType;
    public int Duration;
    public string? MatchedLabelText;
    public string? MatchedLabelPosition;
    public string? LicenceNumber;
    public double? LicenceNumberOcrConfidence;
    public bool LimitsFound;
    public bool MeansFound;
    public string? LinkedLicenceNumbers;
    public int NodeId;
}