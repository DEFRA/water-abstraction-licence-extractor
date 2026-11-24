import {ProcessRun} from '../api/generated/apiClient';

export const DOCUMENT_TYPES: { key: string; label: string }[] = [
    {key: 'AbstractionLicence', label: 'Abstraction Licence'},
    {key: 'WrInspectionReport', label: 'Inspection Report'},
];

export function processRunDocumentType(run: ProcessRun) {
    return run.documentType ?? DOCUMENT_TYPES[0].key;
}

// filesProcessed counts the files the run took a stub lock on, so this is the work list it never
// reached at all.
export function unaccountedFiles(run: ProcessRun) {
    return (run.numberOfFiles ?? 0) - (run.filesProcessed ?? 0);
}

export function processRunFigures(run: ProcessRun) {
    const count = (n?: number) => (n ?? 0).toLocaleString();
    const parts = [`${count(run.filesProcessed)} files`];

    if ((run.filesErrored ?? 0) > 0) {
        parts.push(`${count(run.filesErrored)} errors`);
    }

    if ((run.filesTransientErrored ?? 0) > 0) {
        parts.push(`${count(run.filesTransientErrored)} transient`);
    }

    if (unaccountedFiles(run) > 0) {
        parts.push(`${count(unaccountedFiles(run))} unaccounted`);
    }

    if ((run.licenceCount ?? 0) > 0) {
        parts.push(run.liveLicenceCount === run.licenceCount
            ? `${count(run.licenceCount)} licences`
            : `${count(run.licenceCount)} licences, ${count(run.liveLicenceCount)} live`);
    }

    if (run.status && run.status !== 'Completed') {
        parts.push(run.status);
    }

    return parts.join(' · ');
}
