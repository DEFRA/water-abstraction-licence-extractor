import {waleApiBaseUrl} from './apiClient';
import type {
    DateSource,
    InspectionRecord,
    VerifiedSection,
} from './wrInspectionReportVerificationModel';

interface ApiRow {
    fileId: string;
    processRunId: number;
    matchesResultId: number;
    inspectionDate: string | null;
    formDate: string | null;
    dmsDocumentDate: string | null;
    dmsUploadDate: string | null;
    sortDate: string | null;
    dateSource: DateSource;
    dateWarning: string;
    permitNumber: string | null;
    originalFileName: string | null;
    template: string | null;
    completeness: number;
    isScan: boolean;
}

interface SectionDefinition {
    /// Must be the extracted field's own name: the modal capitalises this and matches it against
    /// matchedLabelName, so a renamed section silently loses its highlight boxes.
    name: string;
    label: string;
    path?: string[];
    isRepeating?: boolean;
    isAbsent?: (value: unknown) => boolean;
}

/// An unmatched provision reads "DidntMatch", which the generic test counts as populated - 6 of 21
/// sampled documents are nothing but DidntMatch. "Blank" is a verdict, so it still counts as present.
function provisionsAreAbsent(value: unknown): boolean {
    if (value === null || typeof value !== 'object' || Array.isArray(value)) {
        return true;
    }

    const verdicts = Object.values(value as Record<string, unknown>);

    return verdicts.length === 0 || verdicts.every(verdict => verdict === 'DidntMatch');
}

/// Declared order, not the form's key order. Excludes "images" and the provenance half of "metadata".
const SECTION_DEFINITIONS: SectionDefinition[] = [
    {name: 'documentHeader', label: 'Document header', path: ['metadata', 'documentHeader']},
    {name: 'licenceNumber', label: 'Licence number'},
    {name: 'licenceNumberCleaned', label: 'Licence number (cleaned)'},
    {name: 'inspectionClass', label: 'Inspection class'},
    {name: 'inspectionDate', label: 'Inspection date'},
    {name: 'address', label: 'Name and address'},
    {name: 'metWith', label: 'Met with'},
    {name: 'inspectingOfficer', label: 'Inspecting officer'},
    {name: 'licenceProvisions', label: 'Licence provisions', isAbsent: provisionsAreAbsent},
    {name: 'measurementDetails', label: 'Measurement details', isRepeating: true},
    {name: 'generalComments', label: 'General comments'},
    {name: 'formSentTo', label: 'Form sent to', path: ['metadata', 'formSentTo']},
    {name: 'date', label: 'Form date', path: ['metadata', 'date']},
];

/// Recurses because the nested cases are the ones that lie: measurementDetails of a document nothing
/// matched is {..., meters: [{}]}, populated-looking at the top level and empty all the way down.
function isEmptyValue(value: unknown): boolean {
    if (value === null || value === undefined || value === '') {
        return true;
    }

    if (Array.isArray(value)) {
        return value.every(isEmptyValue);
    }

    if (typeof value === 'object') {
        return Object.values(value as Record<string, unknown>).every(isEmptyValue);
    }

    return false;
}

function buildSection(definition: SectionDefinition, scraped: unknown): VerifiedSection {
    return {
        name: definition.name,
        label: definition.label,
        isRepeating: definition.isRepeating ?? false,
        scraped,
        override: null,
        verificationType: 'Unverified',
        reExtracted: null,
    };
}

function placeholderSections(): VerifiedSection[] {
    return SECTION_DEFINITIONS.map(definition => buildSection(definition, null));
}

/// Stops at the first step that is not an object, so a form missing all of "metadata" reads as absent.
function readPath(form: Record<string, unknown>, path: string[]): unknown {
    return path.reduce<unknown>(
        (value, key) => value === null || typeof value !== 'object'
            ? undefined
            : (value as Record<string, unknown>)[key],
        form);
}

export function buildSectionsFromForm(form: Record<string, unknown>): VerifiedSection[] {
    return SECTION_DEFINITIONS.map(definition => {
        const value = readPath(form, definition.path ?? [definition.name]);
        const absent = definition.isAbsent ?? isEmptyValue;

        return buildSection(definition, absent(value) ? null : value);
    });
}

/// Decisions live only in memory until there is a store, so re-opening a record carries them by name.
export function mergeSectionDecisions(
    fresh: VerifiedSection[],
    existing: VerifiedSection[]
): VerifiedSection[] {
    return fresh.map(section => {
        const held = existing.find(candidate => candidate.name === section.name);

        return held && held.verificationType !== 'Unverified'
            ? {...section, verificationType: held.verificationType, override: held.override}
            : section;
    });
}

export async function fetchInspectionReportForm(
    fileId: string,
    processRunId: number
): Promise<Record<string, unknown> | null> {
    const response = await fetch(`${waleApiBaseUrl}/BFF/FileData/WrInspectionReportString`
        + `?fileId=${fileId}&processRunId=${processRunId}`);

    if (!response.ok) {
        throw new Error(`WrInspectionReportString returned ${response.status}`);
    }

    const text = await response.text();

    return text ? JSON.parse(text) as Record<string, unknown> : null;
}

export async function fetchInspectionRecords(take = 500): Promise<InspectionRecord[]> {
    const response = await fetch(
        `${waleApiBaseUrl}/BFF/FileData/GetWrInspectionReportsByDate?skip=0&take=${take}`);

    if (!response.ok) {
        throw new Error(`GetWrInspectionReportsByDate returned ${response.status}`);
    }

    const rows = (await response.json()) as ApiRow[];

    return rows.map(row => ({
        fileId: row.fileId,
        processRunId: row.processRunId,
        matchesResultId: row.matchesResultId,
        inspectionDate: row.sortDate ?? row.inspectionDate,
        dateSource: row.dateSource,
        permitNumber: row.permitNumber,
        originalFileName: row.originalFileName,
        template: row.template,
        completeness: row.completeness,
        isScan: row.isScan,
        state: 'Unverified',
        dateWarning: row.dateWarning,
        extractedWithRulesVersion: 'rules-2026.10',
        sections: placeholderSections(),
    }) as InspectionRecord);
}
