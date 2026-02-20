namespace WALE.ProcessFile.Core.Models;

public class NaldDataCollection
{
    public List<NaldAbstractionLicenceDataLine>? Licences { get; set; }
    
    public List<NaldLicence>? LicencesAlternateFormat { get; set; }

    public List<NaldLicenceVersionDataLine>? LicenceVersions { get; set; }

    public List<NaldLicencePurposeDataLine>? LicencePurposes { get; set; }

    public List<NaldLicencePointDataLine>? LicencePoints { get; set; }

    public List<NaldLicenceQuantitiesDataLine>? LicenceQuantities { get; set; }
}