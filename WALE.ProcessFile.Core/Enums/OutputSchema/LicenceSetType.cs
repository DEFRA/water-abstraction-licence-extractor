namespace WALE.ProcessFile.Core.Enums.OutputSchema;

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