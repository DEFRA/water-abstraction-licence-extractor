namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    Unknown,
    SingleLicenceOnly,
    LicencesGroupedByAbstractionLimits,
    LicencesGroupedByLineage,
    LicencesGroupedByOtherThenLimitsAndLinegage,
    
    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere,
    AllLicencesExplicitlyReferencedInLimits,
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn,
    PartiallyEncompassedIn
}