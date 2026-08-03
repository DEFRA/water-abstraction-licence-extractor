using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class LicenceListRow
{
    public long LicenceListItemId { get; set; }

    public int ProcessRunId { get; set; }

    public Guid FileId { get; set; }

    public required string Filename { get; set; }

    public string? LicenceNumber { get; set; }

    public string? LicenceHolder { get; set; }

    public string[] Purposes { get; set; } = [];

    public string[] Points { get; set; } = [];

    public int LimitsCount { get; set; }

    public int AggregatesCount { get; set; }

    public bool Ocr { get; set; }

    public DateTime? IssueDate { get; set; }

    public string? Issuer { get; set; }

    public bool MeansFound { get; set; }

    public string? Status { get; set; }

    public LicenceListLinkedLicenceRow[] LinkedLicences { get; set; } = [];

    public LicenceListLicenceSetRow[] LicenceSets { get; set; } = [];

    public LicenceSectionVerificationSummary[] LicenceSectionVerifications { get; set; } = [];
}