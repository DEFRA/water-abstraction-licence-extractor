namespace WALE.ProcessFile.Services.Models;

public class PdfPageProvider
{
    public string? Provider { get; set; }
    public IReadOnlyList<string>? Text { get; set; } = [];
}