// The shape a verification decision is held in, and the record it belongs to. Named for the
// licence side's own vocabulary (LicenceSection.tsx) rather than a new one, so the two agree on
// what Confirmed and ConfirmNone mean.

export type DateSource = 'Extracted' | 'Metadata' | 'DmsDocument' | 'DmsUpload' | 'None';

export type VerificationType = 'Unverified' | 'Confirmed' | 'Edited' | 'ConfirmNone' | 'BusinessReview';

export type RecordState = 'Unverified' | 'InReview' | 'Verified' | 'BusinessReview';

export interface VerifiedSection {
    name: string;
    label: string;
    /// Repeating sections cannot be diffed positionally, so their rows carry their own ids.
    isRepeating: boolean;
    /// What extraction produced, null when nothing was. Not narrowed to an object: a section is a
    /// property of the extracted form, and those are variously a string, an array and a nested
    /// object.
    scraped: unknown;
    /// What a human put right, when they did.
    override: unknown;
    verificationType: VerificationType;
    /// What a later run produced, held only when it disagrees with the verified value. Re-running a
    /// verified record scores the rules against human truth; it never replaces the verified value.
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
    /// The projection's own plausibility verdict, so the queue does not recompute it.
    dateWarning?: string;

    /// The rules this record was last extracted with. A run only revisits a verified record when
    /// this differs from the current rules.
    extractedWithRulesVersion: string;
    sections: VerifiedSection[];
}

/// Stands in for whatever stamps the compiled rule set today. Nothing does, which is why the page
/// hides the comparison rather than showing every record as current.
export const CURRENT_RULES_VERSION = 'rules-2026.10';

/// The record as it would reach the lake: verified values, the extraction they came from, and
/// enough provenance to recompute accuracy later.
export function buildLakeRecord(record: InspectionRecord) {
    return {
        fileId: record.fileId,
        permitNumber: record.permitNumber,
        documentName: record.originalFileName,
        inspectionDate: record.inspectionDate,
        inspectionDateSource: record.dateSource,
        verifiedAt: new Date().toISOString(),
        // Fixed until a signed-in identity reaches the portal; there is no auth on it today.
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
