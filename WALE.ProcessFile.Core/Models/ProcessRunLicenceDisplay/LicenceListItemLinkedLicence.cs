namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemLinkedLicence
{
    public long LinkedLicenceId { get; set; }

    public long LicenceListItemId { get; set; }

    public string? LicenceNumber { get; set; }

    public string? RawScrapedLicenceNumber { get; set; }

    public string? DmsPermitNumber { get; set; }

    public Guid? DmsFileId { get; set; }

    public string? Filename { get; set; }

    public string? DmsPath { get; set; }

    public string? LicenceVersionId { get; set; }

    public DateTime? EffectiveDate { get; set; }

    public DateTime? IssueDate { get; set; }

    public string? Issuer { get; set; }

    public string? NaldStatus { get; set; }

    public string? LicenceType { get; set; }

    public int? RegionId { get; set; }

    public string? SourceData { get; set; }

    public List<LicenceListItemLinkLocation> Locations { get; init; } = [];
}