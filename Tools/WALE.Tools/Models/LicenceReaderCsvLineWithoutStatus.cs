namespace WALE.Tools.Models;

public class LicenceReaderCsvLineWithoutStatus
{
    public string? LicenceNumber { get; set; }
    public string? PermitNumber { get; set; }
    public string? FileName { get; set; }
    public DateOnly? DateOfIssue { get; set; }
}