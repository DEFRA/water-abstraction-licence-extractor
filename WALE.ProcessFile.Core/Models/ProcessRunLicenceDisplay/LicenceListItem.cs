namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItem
{
    public long LicenceListItemId { get; set; }

    public int ProcessRunId { get; set; }

    public Guid FileId { get; set; }

    public required string Filename { get; set; }

    public string? LicenceNumber { get; set; }

    public string? LicenceHolder { get; set; }

    public int LimitsCount { get; set; }

    public int AggregatesCount { get; set; }

    public bool Ocr { get; set; }

    public DateOnly? IssueDate { get; set; }

    public short? IssueYear { get; set; }

    public string? Issuer { get; set; }

    public bool MeansFound { get; set; }

    public string? Status { get; set; }

    public int PurposesCount { get; set; }

    public int PointsCount { get; set; }

    public string[] Purposes { get; set; } = [];

    public string[] Points { get; set; } = [];
 
    public int LinkedLicencesCount { get; set; }

    public int LicenceSetsCount { get; set; }

    public int VerificationSectionsCount { get; set; }

    public int VerificationItemsCount { get; set; }

    public bool HasVerifications { get; set; }

    public string? SearchText { get; set; }

    /// <summary>
    /// Optional JSON snapshot of the complete source record.
    /// Store as a JSON string when using Dapper/Npgsql.
    /// </summary>
    public string? SourceData { get; set; }

    public DateTimeOffset CreatedDateTimeUtc { get; set; }

    public DateTimeOffset UpdatedDateTimeUtc { get; set; }
}