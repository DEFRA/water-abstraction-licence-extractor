// Use a const object instead of an enum
export const LicenceSetType = {
    SingleLicenceOnly: 0,
    AllLicencesExplicitlyReferencedAnywhere: 1,
    AllLicencesExplicitlyReferencedInLimits: 2,
    AllLicencesImplicitlyReferencedInLimits: 3,
    AllLicencesIncludingImplicitlyReferenced: 4,
    FullyEncompassedIn: 5,
    PartiallyEncompassedIn: 6
} as const;

// Create a type from the object values
export type LicenceSetType = (typeof LicenceSetType)[keyof typeof LicenceSetType];

export const licenceSetTypeClassMap: Record<number, string> = {
    [LicenceSetType.SingleLicenceOnly]: 'singleLicenceOnly',
    [LicenceSetType.AllLicencesExplicitlyReferencedAnywhere]: 'allLicencesExplicitlyReferencedAnywhere',
    [LicenceSetType.AllLicencesExplicitlyReferencedInLimits]: 'allLicencesExplicitlyReferencedInLimits',
    [LicenceSetType.AllLicencesImplicitlyReferencedInLimits]: 'allLicencesImplicitlyReferencedInLimits',
    [LicenceSetType.AllLicencesIncludingImplicitlyReferenced]: 'allLicencesIncludingImplicitlyReferenced',
    [LicenceSetType.FullyEncompassedIn]: 'fullyEncompassedIn',
    [LicenceSetType.PartiallyEncompassedIn]: 'partiallyEncompassedIn'
};

export function getLicenceSetTypeClass(type: number | undefined | null): string {
    if (type === undefined || type === null) return '';
    return licenceSetTypeClassMap[type] ?? '';
}
