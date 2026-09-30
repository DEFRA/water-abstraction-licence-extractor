namespace WALE.Api.Areas.BFF.Models;

public class VerificationExportChunkResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Csv { get; set; } = string.Empty;
    public bool HasMore { get; set; }
    public int Chunk { get; set; }
}