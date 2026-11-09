using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class VerificationProvenance
{
    public static VerifiedBy For(string? verificationType) => verificationType switch
    {
        "Confirmed" or "Edited" or "Added" or "Removed" => VerifiedBy.Human,
        "AutoConfirm" or "AutoWarn" or "AutoFail" => VerifiedBy.Machine,
        "RequestBusinessReview" or "CompleteBusinessReview" => VerifiedBy.BusinessReview,
        _ => VerifiedBy.Unknown
    };
}
