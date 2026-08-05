using WALE.ProcessFile.Core.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Interfaces;

public interface ILicenceNumberService : ILicenceNumberServiceCore
{
    List<NaldLicence> GetNaldLicences(string licenceNumber);
    
    List<NaldLicence> ExtractNaldLicences(string? sourceText);
}