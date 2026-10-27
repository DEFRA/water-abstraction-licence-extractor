// Use a const object instead of an enum
export const LicenceSetType = {
    Unknown: 0,
    SingleLicenceOnly: 1,
    LicencesGroupedByAbstractionLimits: 2,
    Others: 3,

    // Oldies for serialisation
    AllLicencesExplicitlyReferencedAnywhere: 4,
    AllLicencesExplicitlyReferencedInLimits: 5,
    AllLicencesImplicitlyReferencedInLimits: 6,
    AllLicencesIncludingImplicitlyReferenced: 7,
    FullyEncompassedIn: 8,
    PartiallyEncompassedIn: 9
} as const;

// Create a type from the object values
export type LicenceSetType = (typeof LicenceSetType)[keyof typeof LicenceSetType];

export const licenceSetTypeClassMap: Record<number, string> = {
    [LicenceSetType.Unknown]: 'unknown',
    [LicenceSetType.SingleLicenceOnly]: 'singleLicenceOnly',
    [LicenceSetType.LicencesGroupedByAbstractionLimits]: 'licencesGroupedByAbstractionLimits',
    [LicenceSetType.Others]: 'others',
    
    [LicenceSetType.AllLicencesExplicitlyReferencedAnywhere]: 'unknown',
    [LicenceSetType.AllLicencesExplicitlyReferencedInLimits]: 'unknown',
    [LicenceSetType.AllLicencesImplicitlyReferencedInLimits]: 'unknown',
    [LicenceSetType.AllLicencesIncludingImplicitlyReferenced]: 'unknown',
    [LicenceSetType.FullyEncompassedIn]: 'unknown',
    [LicenceSetType.PartiallyEncompassedIn]: 'unknown'
};

export function getLicenceSetTypeClass(type: number | undefined | null): string {
    if (type === undefined || type === null) return '';
    return licenceSetTypeClassMap[type] ?? '';
}