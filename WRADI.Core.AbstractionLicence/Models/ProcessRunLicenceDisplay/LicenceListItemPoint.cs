namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemPoint
{
    public long LicenceListItemPointId { get; set; }

    public long LicenceListItemId { get; set; }

    public required string Point { get; set; }

    public int SortOrder { get; set; }
}