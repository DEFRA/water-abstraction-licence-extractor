namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class UpsertLicenceListItem
{
    public int ProcessRunId { get; init; }

    public Guid FileId { get; init; }

    public string Filename { get; init; } = string.Empty;

    public string? LicenceNumber { get; init; }

    public string? LicenceHolder { get; init; }

    public string[] Purposes { get; init; } = [];

    public string[] Points { get; init; } = [];

    public int LimitsCount { get; init; }

    public int AggregatesCount { get; init; }
    
    public bool NaldAggregate { get; init; }

    public bool Ocr { get; init; }

    public DateOnly? IssueDate { get; init; }

    public string? Issuer { get; init; }

    public bool MeansFound { get; init; }

    public string? Status { get; init; }

    public UpsertLinkedLicenceItem[] LinkedLicences { get; init; } = [];

    public UpsertLicenceSetItem[] LicenceSets { get; init; } = [];

    public LicenceSectionVerificationSummary[] LicenceSectionVerifications { get; init; } = [];

    public string? SourceData { get; init; }
}