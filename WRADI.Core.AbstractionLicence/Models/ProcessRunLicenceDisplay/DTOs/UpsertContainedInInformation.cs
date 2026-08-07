namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class UpsertContainedInInformation
{
    public string Source { get; init; } = string.Empty;

    public string Direction { get; init; } = string.Empty;

    public string? SectionName { get; init; }

    public string? LinkReason { get; init; }

    public int? LineNumber { get; init; }

    public int? PageNumber { get; init; }
    
    public string? AcinCode { get; init; }
    
    public string? SourceFields { get; init; }
}