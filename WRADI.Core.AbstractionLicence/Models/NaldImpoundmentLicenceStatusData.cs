namespace WRADI.Core.AbstractionLicence.Models;

public class NaldImpoundmentLicenceStatusData
{
    // Impoundment licences
    public HashSet<string> LiveLicences { get; set; } = [];

    public HashSet<string> RevokedLicences { get; set; } = [];
    
    public HashSet<string> TerminatedLicences { get; set; } = [];
}