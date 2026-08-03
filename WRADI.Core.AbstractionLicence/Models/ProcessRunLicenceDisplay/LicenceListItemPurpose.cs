namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemPurpose
{
    public long LicenceListItemId { get; set; }

    public required string Purpose { get; set; }

    public int SortOrder { get; set; }
}