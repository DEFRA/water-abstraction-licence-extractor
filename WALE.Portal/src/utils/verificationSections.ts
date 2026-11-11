import {compareAlphanumeric} from "./formatting.ts";

export const VERIFICATION_SECTIONS: readonly string[] = ['Linked Licences', 'Aggregates'];

export const isVerificationSectionSelected = (
    sectionName: string | undefined,
    selectedSections?: string[]
): boolean => {
    if (!selectedSections || selectedSections.length === 0) {
        return true;
    }

    return !!sectionName && selectedSections.includes(sectionName);
};

export const getVerificationSectionsToDisplay = (
    presentSectionNames: (string | undefined)[],
    selectedSections?: string[]
): string[] => {
    const sections = selectedSections && selectedSections.length > 0
        ? selectedSections
        : [...VERIFICATION_SECTIONS, ...presentSectionNames.filter((name): name is string => !!name)];

    return [...new Set(sections)].sort(compareAlphanumeric);
};
