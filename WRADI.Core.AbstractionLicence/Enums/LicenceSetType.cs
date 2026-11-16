namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    Unknown,
    SingleLicenceOnly,
    LicencesGroupedByAbstractionLimits,
    LicencesGroupedByLineage,
    LicencesGroupedByOtherThenLimitsAndLineage,
    
    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere,
    AllLicencesExplicitlyReferencedInLimits,
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn,
    PartiallyEncompassedIn,
    LicencesGroupedByOtherThenLimitsAndLinegage // Mispelling
}