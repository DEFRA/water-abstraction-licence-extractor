using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class VerificationProvenanceTests
{
    /// The nine values verification_type holds in practice, measured 2026-11-10. Anything else
    /// falls to Unknown, so a tenth has to be added here as well as to the write path.
    [Theory]
    [InlineData("Confirmed", VerifiedBy.Human)]
    [InlineData("Edited", VerifiedBy.Human)]
    [InlineData("Added", VerifiedBy.Human)]
    [InlineData("Removed", VerifiedBy.Human)]
    [InlineData("AutoConfirm", VerifiedBy.Machine)]
    [InlineData("AutoWarn", VerifiedBy.Machine)]
    [InlineData("AutoFail", VerifiedBy.Machine)]
    [InlineData("RequestBusinessReview", VerifiedBy.BusinessReview)]
    [InlineData("CompleteBusinessReview", VerifiedBy.BusinessReview)]
    public void EveryStoredVerificationTypeHasAProvenance(string verificationType, VerifiedBy expected)
    {
        Assert.Equal(expected, VerificationProvenance.For(verificationType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SomethingNobodyHasWrittenYet")]
    public void AnUnrecognisedTypeIsUnknown(string? verificationType)
    {
        Assert.Equal(VerifiedBy.Unknown, VerificationProvenance.For(verificationType));
    }

    /// A casing change in the write path would silently demote human truth to Unknown.
    [Theory]
    [InlineData("confirmed")]
    [InlineData("CONFIRMED")]
    [InlineData("autoConfirm")]
    public void TheMatchIsCaseSensitive(string verificationType)
    {
        Assert.Equal(VerifiedBy.Unknown, VerificationProvenance.For(verificationType));
    }
}
