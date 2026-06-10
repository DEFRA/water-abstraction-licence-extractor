using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.BFF.Models;

public record ProcessRunResponse
{
    public required int TotalRecords { get; init; }
    public required IReadOnlyList<OutputListDataItem> Records { get; init; }
}