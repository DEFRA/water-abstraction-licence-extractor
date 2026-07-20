namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public class LicenceListItemAggregate
{
    public required LicenceListItem Licence { get; init; }

    public List<LicenceListItemPurpose> Purposes { get; init; } = [];

    public List<LicenceListItemPoint> Points { get; init; } = [];

    public List<LicenceListItemLinkedLicence> LinkedLicences { get; init; } = [];

    public List<LicenceListItemLicenceSet> LicenceSets { get; init; } = [];

    public List<LicenceListItemVerificationSection> VerificationSections { get; init; } = [];
}