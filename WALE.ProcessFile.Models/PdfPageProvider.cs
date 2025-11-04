namespace WALE.ProcessFile.Models;

public class PdfPageProvider
{
    public string? Provider { get; set; }
    public IReadOnlyList<string>? Text { get; set; } = [];
}