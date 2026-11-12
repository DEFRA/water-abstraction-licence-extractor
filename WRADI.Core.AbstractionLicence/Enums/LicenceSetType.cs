namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    Unknown,
    SingleLicenceOnly,
    LicencesGroupedByAbstractionLimits,
    Others,
    Lineage,
    
    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere,
    AllLicencesExplicitlyReferencedInLimits,
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn,
    PartiallyEncompassedIn
}