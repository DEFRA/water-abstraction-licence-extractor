namespace WALE.ProcessFile.Core.Models;

public class ProcessRunQuery
{
    public string? SearchTerm { get; init; } = string.Empty;

    public string SearchTermClean => (SearchTerm?.Equals("N/A", StringComparison.OrdinalIgnoreCase) == true ? string.Empty : SearchTerm) ?? string.Empty;

    public int Skip { get; set; } = 0;

    public int Take { get; set; } = int.MaxValue;

    public string? Issuer { get; init; }

    public bool? LimitsEmpty { get; init; }

    public bool? AggregatesEmpty { get; init; }

    public bool? OcrScan { get; init; }
    
    public bool? PurposesEmpty { get; init; }
    
    public bool? PointsEmpty { get; init; }

    public int? IssueYear { get; init; }

    public bool? MeansFound { get; init; }

    public string? ShortLicenceSetId { get; init; }

    public string? LinkedLicencesType { get; init; }
    
    public string? VerificationType { get; init; }
}