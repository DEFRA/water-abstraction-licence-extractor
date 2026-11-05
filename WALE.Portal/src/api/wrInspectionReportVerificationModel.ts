// Vocabulary matches the licence side (LicenceSection.tsx): Confirmed and ConfirmNone mean the same.

export type DateSource = 'Extracted' | 'Metadata' | 'DmsDocument' | 'DmsUpload' | 'None';

export type VerificationType = 'Unverified' | 'Confirmed' | 'Edited' | 'ConfirmNone' | 'BusinessReview';

export type RecordState = 'Unverified' | 'InReview' | 'Verified' | 'BusinessReview';

export interface VerifiedSection {
    name: string;
    label: string;
    isRepeating: boolean;
    scraped: unknown;
    override: unknown;
    verificationType: VerificationType;
    /// Held only when a later run disagrees; it never replaces the verified value.
    reExtracted: unknown;
}

export interface InspectionRecord {
    fileId: string;
    processRunId: number;
    matchesResultId: number;
    inspectionDate: string | null;
    dateSource: DateSource;
    permitNumber: string | null;
    originalFileName: string | null;
    template: string | null;
    completeness: number;
    isScan: boolean;
    state: RecordState;
    dateWarning?: string;

    extractedWithRulesVersion: string;
    sections: VerifiedSection[];
}

/// Nothing stamps the compiled rule set yet, which is why the page hides the comparison.
export const CURRENT_RULES_VERSION = 'rules-2026.10';

export function buildLakeRecord(record: InspectionRecord) {
    return {
        fileId: record.fileId,
        permitNumber: record.permitNumber,
        documentName: record.originalFileName,
        inspectionDate: record.inspectionDate,
        inspectionDateSource: record.dateSource,
        verifiedAt: new Date().toISOString(),
        // Fixed until the portal has a signed-in identity.
        verifiedBy: 'edward.butler@environment-agency.gov.uk',
        provenance: {
            processRunId: record.processRunId,
            matchesResultId: record.matchesResultId,
            template: record.template,
            isScan: record.isScan,
            extractionCompleteness: record.completeness,
        },
        sections: Object.fromEntries(
            record.sections.map(section => [
                section.name,
                {
                    value: section.override ?? section.scraped,
                    extracted: section.scraped,
                    verification: section.verificationType,
                },
            ])
        ),
    };
}
