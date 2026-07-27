namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class UpsertLinkLocationItem
{
    public string? Source { get; init; }

    public string? Direction { get; init; }

    public string? SectionName { get; init; }

    public string? LinkReason { get; init; }

    public bool? IsBecauseOfAggregate { get; init; }

    public int? LineNumber { get; init; }

    public int? PageNumber { get; init; }
}