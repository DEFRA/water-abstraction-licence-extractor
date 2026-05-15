namespace WALE.ProcessFile.Core.Models;

public class NaldAbstractionLicenceDataLine
{
    public int Id { get; set; }
    public string? LicenceNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? RevDate { get; set; }
    public DateTime? OrigEffectiveDate { get; set; }
    public DateTime? OrigSignatureDate { get; set; }
    public DateTime? LapsedDate { get; set; }
    public string? ArepEiucCode { get; set; }
    public short FgacRegionCode { get; set; }
}