using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface ILicenceNumberService : WALE.ProcessFile.Core.Interfaces.ILicenceNumberService
{
    List<NaldLicence> GetNaldLicences(string licenceNumber);
    
    List<NaldLicence> ExtractNaldLicences(string? sourceText);
}