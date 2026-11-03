using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.BFF.Models;

/// Not ProcessRunResponse: its CumulativeFilterCounts defaults to new(), so the estate would
/// report a full set of zeros as though it had counted them.
public record LicenceEstateResponse
{
    public required int TotalRecords { get; init; }

    public required IReadOnlyList<OutputListDataItem> Records { get; init; }

    public string[]? Issuers { get; init; }

    public string[]? IssueDates { get; init; }

    public string[]? LicenceSetIds { get; init; }

    public string[]? Statuses { get; init; }

    /// Counted under the filters but not under the queue selection, so the bar stays whole
    /// when one queue is picked.
    public Dictionary<int, int>? QueueCounts { get; init; }
}
