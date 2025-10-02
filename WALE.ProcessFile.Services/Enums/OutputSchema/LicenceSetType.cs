namespace WALE.ProcessFile.Services.Enums.OutputSchema;

public enum LicenceSetType
{
    SingleLicenceOnly,
    AllLicencesExplicitlyReferencedAnywhere,
    AllLicencesExplicitlyReferencedInLimits,
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn
}