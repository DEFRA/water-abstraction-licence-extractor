namespace WALE.ProcessFile.Core.Models;

public class NaldDataCollection
{
    public List<NaldAbstractionLicenceCsvLine>? Licences { get; set; }

    public List<NaldLicenceVersionCsvLine>? LicenceVersions { get; set; }

    public List<NaldLicencePurposeCsvLine>? LicencePurposes { get; set; }

    public List<NaldLicencePointCsvLine>? LicencePoints { get; set; }

    public List<NaldLicenceQuantitiesCsvLine>? LicenceQuantities { get; set; }
}