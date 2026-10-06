namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    SingleLicenceOnly,
    AllLicencesExplicitlyReferencedAnywhere, // TODO combine this and the one 3 down
    AllLicencesExplicitlyReferencedInLimits, // TODO combine this and the below - variations takes care of the difference
    AllLicencesImplicitlyReferencedInLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn, // TODO is this used?
    PartiallyEncompassedIn // TODO is this used?
}