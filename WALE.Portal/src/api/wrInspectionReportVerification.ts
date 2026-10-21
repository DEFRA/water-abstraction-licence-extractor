// The record list comes from the projection; a record's sections are the properties of its
// extracted form, which the projection does not carry, so they are loaded when a record is opened.
// Verification state does not persist yet, so every section arrives Unverified.
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

/// Declared rather than taken from the form's key order, so a reviewer meets the fields in the
/// same order whatever the template produced. Excludes "images" and the provenance half of
/// "metadata"; the three answers nested under metadata are included by path.
interface SectionDefinition {
    /// Must be the extracted field's own name: the modal resolves a section's highlight boxes by
    /// capitalising this and looking it up against matchedLabelName, so a renamed section silently
    /// loses its page region. These are the leaf names even where the value is nested.
    name: string;
    label: string;
    /// Where the value sits in the form, defaulting to a top-level property of the same name.
    /// Given explicitly for the answers the converter nests under "metadata".
    path?: string[];
    isRepeating?: boolean;
    /// Replaces the generic emptiness test where a property has its own idea of "nothing found".
    isAbsent?: (value: unknown) => boolean;
}

/// A provisions block always arrives with all thirteen keys, and an unmatched provision reads
/// "DidntMatch" - a real string the generic test counts as populated. 6 of 21 sampled documents
/// are nothing but DidntMatch. "Blank" still counts as present: that is a verdict, not a failure.
function provisionsAreAbsent(value: unknown): boolean {
    if (value === null || typeof value !== 'object' || Array.isArray(value)) {
        return true;
    }

    const verdicts = Object.values(value as Record<string, unknown>);

    return verdicts.length === 0 || verdicts.every(verdict => verdict === 'DidntMatch');
}

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
    // meters is a list of records, so a later per-row diff cannot align them positionally.
    {name: 'measurementDetails', label: 'Measurement details', isRepeating: true},
    {name: 'generalComments', label: 'General comments'},
    // The form's footer: who it was sent to and the date it was completed, which is a separate
    // field from the inspection date and regularly misread - "24/8/017" parses to the year 17.
    {name: 'formSentTo', label: 'Form sent to', path: ['metadata', 'formSentTo']},
    {name: 'date', label: 'Form date', path: ['metadata', 'date']},
];

/// Absence has four shapes in the serialised form, and the nested ones are why this recurses: a
/// missing scalar is omitted entirely, an unmatched object is "{}", an unmatched list is "[]", and
/// measurementDetails of a document nothing matched is {..., meters: [{}]} - populated-looking at
/// the top level and empty all the way down.
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

/// Every record carries the same set, so the list can show a real denominator before any form is
/// fetched.
function placeholderSections(): VerifiedSection[] {
    return SECTION_DEFINITIONS.map(definition => buildSection(definition, null));
}

/// Walks a definition's path, stopping at the first step that is not an object so a form missing
/// the whole of "metadata" reads as an absent value rather than throwing.
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

/// Re-opening a record rebuilds its sections from the form, so decisions taken in this session -
/// which live only in memory until there is a store - are carried across by section name.
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
        // Carried through so the queue can bucket on it rather than recomputing from the date.
        dateWarning: row.dateWarning,
        extractedWithRulesVersion: 'rules-2026.10',
        sections: placeholderSections(),
    }) as InspectionRecord);
}
