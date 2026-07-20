namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class CreateLinkedLicence
{
    public string? LicenceNumber { get; init; }

    public string? RawScrapedLicenceNumber { get; init; }

    public string? DmsPermitNumber { get; init; }

    public Guid? DmsFileId { get; init; }

    public string? Filename { get; init; }

    public string? DmsPath { get; init; }

    public string? LicenceVersionId { get; init; }

    public DateOnly? EffectiveDate { get; init; }

    public DateOnly? IssueDate { get; init; }

    public string? Issuer { get; init; }

    public string? NaldStatus { get; init; }

    public string? LicenceType { get; init; }

    public int? RegionId { get; init; }

    public CreateLinkLocation[] ContainedIn { get; init; } = [];

    public string? SourceData { get; init; }
}

public sealed class CreateLinkLocation
{
    public string? Source { get; init; }

    public string? Direction { get; init; }

    public string? SectionName { get; init; }

    public string? LinkReason { get; init; }

    public bool? IsBecauseOfAggregate { get; init; }

    public int? LineNumber { get; init; }

    public int? PageNumber { get; init; }
}