namespace WRADI.Core.AbstractionLicence.Models;

public class NationalGridReference
{
    public int ReferenceIndex { get; set; }
    public string? Sheet { get; set; }
    public string? East { get; set; }
    public string? North { get; set; }

    public override string ToString()
    {
        return $"{Sheet} {East} {North}";
    }
}