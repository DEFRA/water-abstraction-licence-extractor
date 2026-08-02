namespace WRADI.Core.AbstractionLicence.Models;

public class NaldDataCollection
{
    public List<NaldAbstractionLicenceDataLine>? AbstractionLicences { get; set; }
    
    public List<NaldLicence>? AbstractionAndImpoundmentLicences { get; set; }

    public List<NaldLicenceVersionDataLine>? AbstractionLicenceVersions { get; set; }

    public List<NaldLicencePurposeDataLine>? AbstractionLicencePurposes { get; set; }

    public List<NaldLicencePointDataLine>? AbstractionLicencePoints { get; set; }

    public List<NaldLicenceQuantitiesDataLine>? AbstractionLicenceQuantities { get; set; }
}