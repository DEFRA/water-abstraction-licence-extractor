namespace WALE.Tools.Models;

public class LicenceReaderCsvLine : LicenceReaderCsvLineWithoutStatus
{
    public string? ProcessingStatus { get; set; }
}