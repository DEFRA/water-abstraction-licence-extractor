using System.Collections.Generic;

namespace WRADI.Services.LicenceFinder.Models;

public class NaldDataCollection
{
    public List<NaldAbstractionLicenceDataLine>? AbstractionLicences { get; set; }
    public List<NaldLicenceVersionDataLine>? AbstractionLicenceVersions { get; set; }
}