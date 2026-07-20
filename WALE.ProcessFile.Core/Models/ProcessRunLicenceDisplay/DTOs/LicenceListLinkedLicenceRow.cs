namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class LicenceListLinkedLicenceRow
{
    public long LinkedLicenceId { get; set; }

    public string? LicenceNumber { get; set; }

    public string? RawScrapedLicenceNumber { get; set; }

    public string? DmsPermitNumber { get; set; }

    public Guid? DmsFileId { get; set; }

    public string? Filename { get; set; }

    public string? DmsPath { get; set; }

    public string? LicenceVersionId { get; set; }

    public DateOnly? EffectiveDate { get; set; }

    public DateOnly? IssueDate { get; set; }

    public string? Issuer { get; set; }

    public string? NaldStatus { get; set; }

    public string? LicenceType { get; set; }

    public int? RegionId { get; set; }

    public LicenceListLinkLocationRow[] ContainedIn { get; set; } = [];
}