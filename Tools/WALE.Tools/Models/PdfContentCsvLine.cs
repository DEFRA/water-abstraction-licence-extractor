namespace WALE.Tools.Models;

public class PdfContentCsvLine
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Headers { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string LicenseNumbers { get; set; } = string.Empty;
    public DateTime ProcessingDate { get; set; }
}