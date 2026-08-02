namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    SingleLicenceOnly,
    AllLicencesExplicitlyReferencedAnywhere,
    AllLicencesExplicitlyReferencedInLimits,
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn,
    PartiallyEncompassedIn
}