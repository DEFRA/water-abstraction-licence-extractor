namespace WALE.ProcessFile.Core.Models;

public class StoredDocumentText
{
    public string SourceService { get; set; } = string.Empty;

    public int FormatVersion { get; set; }

    public string Text { get; set; } = string.Empty;
}
