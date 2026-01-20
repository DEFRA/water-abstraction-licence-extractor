namespace WALE.ProcessFile.Core.Models;

public class NaldLicenceStatusData
{
    public HashSet<string> LiveLicences { get; set; } = [];

    public HashSet<string> DeadLicences { get; set; } = [];
    
    public HashSet<string> ImpoundmentLicences { get; set; } = [];
}