namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    Unknown = 0,
    SingleLicenceOnly = 1,
    LicencesGroupedByAbstractionLimits = 2,
    LicencesGroupedByLineage = 3,
    LicencesGroupedByOtherThenLimitsAndLineage = 4,
    
    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere = 5,
    AllLicencesExplicitlyReferencedInLimits = 6,
    AllLicencesImplicitlyReferencedInLimits = 7,
    AllLicencesIncludingImplicitlyReferenced = 8,
    FullyEncompassedIn = 9,
    PartiallyEncompassedIn = 10,
    LicencesGroupedByOtherThenLimitsAndLinegage = 11, // Mispelling
    Others = 12,
    Lineage = 13
}
