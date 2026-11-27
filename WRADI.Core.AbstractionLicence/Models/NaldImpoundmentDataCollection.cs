namespace WRADI.Core.AbstractionLicence.Models;

public class NaldImpoundmentDataCollection
{
    public List<NaldLicence>? ImpoundmentLicences { get; set; }
    
    public List<NaldImpoundmentLicenceVersionDataLine>? ImpoundmentLicenceVersions { get; set; }
}