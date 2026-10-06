namespace WRADI.Core.AbstractionLicence.Enums;

public enum LicenceSetType
{
    SingleLicenceOnly,
    AllLicencesExplicitlyReferencedAnywhere, // TODO combine this and the one 3 down
    LicencesGroupedByAbstractionLimits,
    AllLicencesIncludingImplicitlyReferenced,
    FullyEncompassedIn, // TODO is this used?
    PartiallyEncompassedIn // TODO is this used?
}