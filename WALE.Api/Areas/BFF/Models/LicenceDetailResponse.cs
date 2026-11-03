using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.BFF.Models;

public record LicenceDetailResponse
{
    public required string LicenceNumber { get; init; }

    public required IReadOnlyList<LicenceRunHistoryItem> Runs { get; init; }

    public required IReadOnlyList<LicenceSectionVerification> Verifications { get; init; }

    /// The whole chain, not just the licence that directly succeeds this one.
    public string[]? Successors { get; init; }

    /// Estate licences differing only in punctuation, never merged: two licences can
    /// legitimately read alike.
    public string[]? PossibleVariants { get; init; }
}
