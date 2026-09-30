namespace WALE.Api.Areas.BFF.Models;

public class VerificationConfig
{
    public string? PostgresqlHost { get; set; }

    public int ChunkSize { get; set; } = 100;
}