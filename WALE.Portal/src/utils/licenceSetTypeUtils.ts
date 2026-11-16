// Use a const object instead of an enum
export const LicenceSetType = {
    Unknown: 0,
    SingleLicenceOnly : 1,
    LicencesGroupedByAbstractionLimits : 2,
    LicencesGroupedByLineage : 3,
    LicencesGroupedByOtherThenLimitsAndLineage : 4,

    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere: 5,
    AllLicencesExplicitlyReferencedInLimits: 6,
    AllLicencesImplicitlyReferencedInLimits: 7,
    AllLicencesIncludingImplicitlyReferenced: 8,
    FullyEncompassedIn: 9,
    PartiallyEncompassedIn: 10,
    LicencesGroupedByOtherThenLimitsAndLinegage: 11,
    Others: 12,
    Lineage: 13,
} as const;

// Create a type from the object values
export type LicenceSetType = (typeof LicenceSetType)[keyof typeof LicenceSetType];

export const licenceSetTypeClassMap: Record<number, string> = {
    [LicenceSetType.Unknown]: 'unknown',
    [LicenceSetType.SingleLicenceOnly]: 'singleLicenceOnly',
    [LicenceSetType.LicencesGroupedByAbstractionLimits]: 'licencesGroupedByAbstractionLimits',
    [LicenceSetType.LicencesGroupedByLineage]: 'licencesGroupedByLineage',
    [LicenceSetType.LicencesGroupedByOtherThenLimitsAndLineage]: 'licencesGroupedByOtherThenLimitsAndLineage',
    
    [LicenceSetType.AllLicencesExplicitlyReferencedAnywhere]: 'unknown',
    [LicenceSetType.AllLicencesExplicitlyReferencedInLimits]: 'unknown',
    [LicenceSetType.AllLicencesImplicitlyReferencedInLimits]: 'unknown',
    [LicenceSetType.AllLicencesIncludingImplicitlyReferenced]: 'unknown',
    [LicenceSetType.FullyEncompassedIn]: 'unknown',
    [LicenceSetType.PartiallyEncompassedIn]: 'unknown',
    [LicenceSetType.LicencesGroupedByOtherThenLimitsAndLinegage]: 'licencesGroupedByOtherThenLimitsAndLineage',
    [LicenceSetType.Others]: 'licencesGroupedByOtherThenLimitsAndLineage',
    [LicenceSetType.Lineage]: 'licencesGroupedByLineage'
};

export function getLicenceSetTypeClass(type: number | undefined | null): string {
    if (type === undefined || type === null) return '';
    return licenceSetTypeClassMap[type] ?? '';
}