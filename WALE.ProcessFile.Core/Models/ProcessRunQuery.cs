namespace WALE.ProcessFile.Core.Models;

public class ProcessRunQuery
{
    public string? SearchTerm { get; init; } = string.Empty;

    public string SearchTermClean =>
        (SearchTerm?.Equals("N/A", StringComparison.OrdinalIgnoreCase) == true
            ? string.Empty : SearchTerm)
            ?? string.Empty;

    public int Skip { get; set; } = 0;

    public int Take { get; set; } = int.MaxValue;

    public string? Issuer { get; init; }

    public bool? LimitsEmpty { get; init; }

    public string? AggregatesFilter { get; init; }

    public bool? OcrScan { get; init; }
    
    public bool? PurposesEmpty { get; init; }
    
    public bool? PointsEmpty { get; init; }

    public int? IssueYear { get; init; }

    public bool? MeansFound { get; init; }

    public bool? IsLicenceNumberFlagged { get; init; }

    public bool? IsIssueDateFlagged { get; init; }

    public string? ShortLicenceSetId { get; init; }

    public string? LinkedLicencesType { get; init; }
    
    public string? VerificationType { get; init; }
    public string? SortField { get; init; }
    public bool SortAscending { get; init; }
    
    public string[]? LicenceNumbers { get; init; }

    public ProcessRunQuery Clone()
    {
        return new ProcessRunQuery
        {
            SearchTerm = SearchTerm,
            Skip = Skip,
            Take = Take,
            Issuer = Issuer,
            LimitsEmpty = LimitsEmpty,
            AggregatesFilter = AggregatesFilter,
            OcrScan = OcrScan,
            PurposesEmpty = PurposesEmpty,
            PointsEmpty = PointsEmpty,
            IssueYear = IssueYear,
            MeansFound = MeansFound,
            IsLicenceNumberFlagged = IsLicenceNumberFlagged,
            IsIssueDateFlagged = IsIssueDateFlagged,
            ShortLicenceSetId = ShortLicenceSetId,
            LinkedLicencesType = LinkedLicencesType,
            VerificationType = VerificationType,
            SortField = SortField,
            SortAscending = SortAscending,
            LicenceNumbers = LicenceNumbers
        };
    }
}