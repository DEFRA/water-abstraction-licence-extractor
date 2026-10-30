using System.Collections.Generic;

namespace WRADI.Services.LicenceFinder.Models;

public class NaldLicenceStatusData
{
    // Abstraction licences
    public HashSet<string> LiveLicences { get; set; } = [];

    public HashSet<string> RevokedLicences { get; set; } = [];
    
    public HashSet<string> LapsedLicences { get; set; } = [];
    
    public HashSet<string> ExpiredLicences { get; set; } = [];
    
    // Impoundment licences
    public HashSet<string> ImpoundmentLicences { get; set; } = [];
}