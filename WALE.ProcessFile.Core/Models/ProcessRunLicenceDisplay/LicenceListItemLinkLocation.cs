namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemLinkLocation
{
    public long LinkLocationId { get; set; }

    public long LinkedLicenceId { get; set; }

    public string? Source { get; set; }

    public string? Direction { get; set; }

    public string? SectionName { get; set; }

    public string? LinkReason { get; set; }

    public bool? IsBecauseOfAggregate { get; set; }

    public int? LineNumber { get; set; }

    public int? PageNumber { get; set; }
}