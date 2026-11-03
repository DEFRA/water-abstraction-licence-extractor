namespace WRADI.Core.AbstractionLicence.Models;

public sealed class LicenceRunHistoryItem
{
    public int ProcessRunId { get; set; }

    public long LicenceListItemId { get; set; }

    public int LicenceId { get; set; }

    public int MatchesResultId { get; set; }

    public Guid FileId { get; set; }

    public required string Filename { get; set; }

    public string? Status { get; set; }

    public int Queue { get; set; }

    public int LimitsCount { get; set; }

    public int PointsCount { get; set; }

    public int PurposesCount { get; set; }

    public int AggregatesCount { get; set; }

    public int LinkedLicencesCount { get; set; }

    public int LicenceSetsCount { get; set; }

    public bool MeansFound { get; set; }

    public bool Ocr { get; set; }

    public DateOnly? IssueDate { get; set; }

    public bool IsLicenceNumberFlagged { get; set; }

    public bool IsIssueDateFlagged { get; set; }

    public DateTimeOffset CreatedDateTimeUtc { get; set; }
}
