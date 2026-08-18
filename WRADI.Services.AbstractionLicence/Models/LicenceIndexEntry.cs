using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Models;

public class LicenceIndexEntry
{
    public required NaldLicence NaldLicence { get; init; }
        
    public required List<string> Segments { get; init; }
}