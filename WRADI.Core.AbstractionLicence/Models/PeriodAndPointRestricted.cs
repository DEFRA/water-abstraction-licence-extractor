namespace WRADI.Core.AbstractionLicence.Models;

public class PeriodAndPointRestricted
{
    public Point[]? Points { get; set; }
    
    public Purpose[]? Purposes { get; set; }

    protected static void CloneProperties(
        PeriodAndPointRestricted source,
        PeriodAndPointRestricted destination)
    {
        destination.Points = source.Points?.ToArray();
        destination.Purposes = source.Purposes?.ToArray();
    }
}