import {useEffect, useMemo, useState} from 'react';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import InspectionReportVerificationModal from '../components/InspectionReportVerificationModal';
import {
    buildSectionsFromForm,
    fetchInspectionRecords,
    fetchInspectionReportForm,
    mergeSectionDecisions,
} from '../api/wrInspectionReportVerification';
import {
    buildLakeRecord,
    CURRENT_RULES_VERSION,
    type InspectionRecord,
    type RecordState,
    type VerificationType,
} from '../api/wrInspectionReportVerificationModel';

// The whole run: the projection serves it in one query, and the queues are only meaningful
// over the full corpus.
const FETCH_LIMIT = 20000;

// Rendered per queue. The counts are over everything; this caps the DOM, not the data.
const MAX_ROWS_RENDERED = 200;

/// Designed but not built, and each blocked on the same thing: somewhere for a decision or a rules
/// version to live. Kept out of the page rather than shown inert, because a control that can never
/// do anything reads as a defect. The code behind each is intact - flip a flag when its backing
/// lands.
///
/// Nothing dispatches a re-extraction, so the re-run queue only ever accumulated ids in memory.
const SHOW_RE_RUN_CONTROLS = false;
/// Nothing stamps the rule set a record was extracted with, so every record compares as current.
const SHOW_RULES_VERSION = false;
/// Needs a stored verified value for a later run to disagree with, so this queue cannot fill.
const SHOW_DISAGREEMENT_QUEUE = false;

const LOW_COMPLETENESS = 60;
const LOW_CONFIDENCE_TEMPLATES = new Set(['unknown', 'nonStandardNarrative']);


type QueueKey = 'disagreement' | 'noDate' | 'implausibleDate' | 'unknownTemplate' | 'lowCompleteness' | 'dmsDateOnly' | 'ready' | 'verified';

/// A verified record a later run now reads differently.
const disagrees = (record: InspectionRecord) =>
    record.sections.some(section => section.reExtracted !== null);

/// Re-running only earns its place when the rules have moved on.
const staleRules = (record: InspectionRecord) =>
    record.extractedWithRulesVersion !== CURRENT_RULES_VERSION;

interface Queue {
    key: QueueKey;
    label: string;
    matches: (record: InspectionRecord) => boolean;
}

// Ordered worst-first: a record appears in the first bucket it qualifies for, so a reviewer is
// never shown the same document under two reasons.
const QUEUES: Queue[] = [
    {key: 'disagreement', label: 'Re-run disagrees', matches: disagrees},
    {key: 'noDate', label: 'No date extracted', matches: r => r.dateSource === 'None'},
    {
        key: 'implausibleDate',
        label: 'Implausible date',
        matches: r => (r.dateWarning ?? 'None') !== 'None',
    },
    {
        key: 'unknownTemplate',
        label: 'Unknown template',
        matches: r => LOW_CONFIDENCE_TEMPLATES.has(r.template ?? ''),
    },
    {key: 'lowCompleteness', label: 'Low completeness', matches: r => r.completeness < LOW_COMPLETENESS},
    {
        key: 'dmsDateOnly',
        label: 'DMS date only',
        matches: r => r.dateSource === 'DmsDocument' || r.dateSource === 'DmsUpload',
    },
    {key: 'ready', label: 'Ready to verify', matches: () => true},
];

const ACTIVE_QUEUES = QUEUES.filter(queue =>
    queue.key !== 'disagreement' || SHOW_DISAGREEMENT_QUEUE);

const STATE_COLOURS: Record<RecordState, string> = {
    Unverified: '#777',
    InReview: '#b8860b',
    Verified: '#2e7d32',
    BusinessReview: '#c62828',
};

function InspectionReportVerificationPage() {
    const [records, setRecords] = useState<InspectionRecord[]>([]);
    const [loading, setLoading] = useState(true);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [chosenQueue, setChosenQueue] = useState<QueueKey | null>(null);
    const [openFileId, setOpenFileId] = useState<string | null>(null);
    const [openForm, setOpenForm] = useState<Record<string, unknown> | null>(null);
    const [openFormLoading, setOpenFormLoading] = useState(false);
    const [openFormError, setOpenFormError] = useState<string | null>(null);
    const [lakePreviewFileId, setLakePreviewFileId] = useState<string | null>(null);
    const [templateFilter, setTemplateFilter] = useState('');
    const [reRunQueue, setReRunQueue] = useState<string[]>([]);

    const toHome = () => { window.location.href = '/'; };

    useEffect(() => {
        fetchInspectionRecords(FETCH_LIMIT)
            .then(setRecords)
            .catch(error => setLoadError(error instanceof Error ? error.message : 'Failed to load'))
            .finally(() => setLoading(false));
    }, []);

    // Each record lands in exactly one bucket, verified ones out of the work queues entirely.
    const filtered = useMemo(
        () => templateFilter === ''
            ? records
            : records.filter(record => record.template === templateFilter),
        [records, templateFilter]
    );

    const bucketed = useMemo(() => {
        const buckets = new Map<QueueKey, InspectionRecord[]>(ACTIVE_QUEUES.map(queue => [queue.key, []]));
        buckets.set('verified', []);

        for (const record of filtered) {
            if (record.state === 'Verified' && !disagrees(record)) {
                buckets.get('verified')!.push(record);
                continue;
            }

            buckets.get(ACTIVE_QUEUES.find(candidate => candidate.matches(record))!.key)!.push(record);
        }

        return buckets;
    }, [filtered]);

    /// The filter offers the templates the loaded records actually carry. A hardcoded list had
    /// drifted from the corpus in both directions: it offered t2, t3 and v3, which no record on the
    /// local run has, and omitted t6, t7 and impounding - making 2,089 of 15,080 records
    /// unreachable by the filter.
    const templates = useMemo(
        () => [...new Set(records.map(record => record.template))]
            .filter((template): template is string => template !== null && template !== '')
            .sort((left, right) => left.localeCompare(right)),
        [records]
    );

    /// Which queue opens. Whether any given queue has records depends on the corpus - "No date
    /// extracted" is empty on the whole local run - and landing on an empty one reads as a page
    /// that failed to load, so the first queue holding anything wins until a reviewer picks one.
    /// Derived rather than set from an effect, which this project's lint rules disallow.
    const defaultQueue = useMemo<QueueKey>(
        () => ACTIVE_QUEUES.find(queue => (bucketed.get(queue.key) ?? []).length > 0)?.key ?? 'ready',
        [bucketed]
    );

    const activeQueue = chosenQueue ?? defaultQueue;

    const visible = useMemo(
        () => [...(bucketed.get(activeQueue) ?? [])].sort((left, right) =>
            (right.inspectionDate ?? '').localeCompare(left.inspectionDate ?? '')),
        [bucketed, activeQueue]
    );

    const openRecord = records.find(record => record.fileId === openFileId) ?? null;

    /// A record's sections are the properties of its extracted form, which the list query does not
    /// carry, so opening one fetches it. Every open goes through here - the row button and
    /// "Verify & next" alike - or a record would open with an empty rail.
    const openRecordById = (fileId: string | null) => {
        setOpenFileId(fileId);
        setOpenForm(null);
        setOpenFormError(null);

        const record = fileId === null
            ? undefined
            : records.find(candidate => candidate.fileId === fileId);

        if (!record) {
            return;
        }

        setOpenFormLoading(true);

        fetchInspectionReportForm(record.fileId, record.processRunId)
            .then(form => {
                setOpenForm(form);

                if (form === null) {
                    return;
                }

                const sections = buildSectionsFromForm(form);

                setRecords(current => current.map(candidate => candidate.fileId === record.fileId
                    ? {...candidate, sections: mergeSectionDecisions(sections, candidate.sections)}
                    : candidate));
            })
            .catch(error => setOpenFormError(
                error instanceof Error ? error.message : 'Failed to load the extracted report'))
            .finally(() => setOpenFormLoading(false));
    };
    const lakeRecord = records.find(record => record.fileId === lakePreviewFileId) ?? null;

    const verifySection = (
        fileId: string,
        sectionName: string,
        verificationType: VerificationType,
        override?: unknown
    ) => {
        setRecords(current => current.map(record => {
            if (record.fileId !== fileId) return record;

            const sections = record.sections.map(section => section.name === sectionName
                ? {
                    ...section,
                    verificationType,
                    // The extraction is kept whatever the correction, so the pair stays available
                    // as ground truth.
                    override: verificationType === 'Edited' ? (override ?? section.override) : null,
                }
                : section);

            const outstanding = sections.some(section => section.verificationType === 'Unverified');

            return {...record, sections, state: outstanding ? 'InReview' : 'Verified'};
        }));
    };

    const flagRecord = (fileId: string) => {
        setRecords(current => current.map(record =>
            record.fileId === fileId ? {...record, state: 'BusinessReview'} : record));
        openRecordById(null);
    };

    // Straight into the next record in the same queue, which is the point of a queue.
    const openNextInQueue = () => {
        const index = visible.findIndex(record => record.fileId === openFileId);
        const next = visible.slice(index + 1).find(record => record.state !== 'Verified');

        openRecordById(next?.fileId ?? null);
    };

    const verifiedCount = bucketed.get('verified')?.length ?? 0;

    return (
        <div className="list-page-container">
            <div style={{position: 'absolute', top: 5, left: 5, cursor: 'pointer'}} onClick={toHome}>&#8617;</div>

            <h1>WR51 Verification</h1>

            <div style={{
                background: '#fff8e1', border: '1px solid #e6c200', borderRadius: '3px',
                padding: '6px 10px', margin: '8px 0', fontSize: '12px',
            }}>
                Decisions are held in this page only - there is no verification store yet, so a
                reload loses them.
            </div>

            {loading && <p>Loading from the projection...</p>}
            {loadError && <p className="error">Could not load records: {loadError}</p>}


            <div style={{margin: '10px 0', display: 'flex', gap: '12px', alignItems: 'center'}}>
                <label>
                    Template:{' '}
                    <select value={templateFilter} onChange={event => setTemplateFilter(event.target.value)}>
                        <option value="">all</option>
                        {templates.map(template => (
                            <option key={template} value={template}>{template}</option>
                        ))}
                    </select>
                </label>

                {SHOW_RE_RUN_CONTROLS && (
                    <>
                        <button
                            onClick={() => setReRunQueue(visible.filter(staleRules).map(record => record.fileId))}
                            disabled={visible.filter(staleRules).length === 0}
                            title="Queue every record in this view whose rules version is behind the current one">
                            Re-run stale ({visible.filter(staleRules).length})
                        </button>

                        {reRunQueue.length > 0 && (
                            <button onClick={() => setReRunQueue([])}>Clear re-run queue</button>
                        )}
                    </>
                )}
            </div>

            <div style={{display: 'flex', gap: '6px', flexWrap: 'wrap', margin: '10px 0'}}>
                {[...ACTIVE_QUEUES, {key: 'verified' as QueueKey, label: 'Verified'}].map(queue => {
                    const count = bucketed.get(queue.key)?.length ?? 0;
                    const isActive = activeQueue === queue.key;

                    return (
                        <button
                            key={queue.key}
                            onClick={() => setChosenQueue(queue.key)}
                            style={{
                                padding: '5px 10px',
                                cursor: 'pointer',
                                border: isActive ? '2px solid #1565c0' : '1px solid #bbb',
                                background: isActive ? '#e3f2fd' : '#fafafa',
                                fontWeight: isActive ? 600 : 400,
                            }}>
                            {queue.label} ({count})
                        </button>
                    );
                })}
            </div>

            <div style={{marginBottom: '6px', fontSize: '12px'}}>
                {visible.length} record(s) in this queue
                {visible.length > MAX_ROWS_RENDERED && <> (showing the first {MAX_ROWS_RENDERED})</>} &nbsp;|&nbsp; {verifiedCount} verified
                of {filtered.length}
                {templateFilter !== '' && <> (filtered from {records.length})</>}
                {SHOW_RE_RUN_CONTROLS && reRunQueue.length > 0 && (
                    <> &nbsp;|&nbsp; <strong>{reRunQueue.length} queued for re-run</strong></>
                )}
            </div>

            <table className="licences-table" style={{width: '100%', fontSize: '12px'}}>
                <thead>
                    <tr>
                        <th>Inspection</th>
                        <th>Src</th>
                        <th>Permit</th>
                        <th>DMS name</th>
                        <th>Tmpl</th>
                        <th>Compl</th>
                        <th>Sections</th>
                        {SHOW_RULES_VERSION && <th>Rules</th>}
                        <th>State</th>
                        <th/>
                    </tr>
                </thead>
                <tbody>
                    {visible.slice(0, MAX_ROWS_RENDERED).map(record => {
                        const done = record.sections.filter(s => s.verificationType !== 'Unverified').length;

                        return (
                            <tr key={record.fileId}>
                                <td>{record.inspectionDate ?? <em style={{color: '#c62828'}}>none</em>}</td>
                                <td>{record.dateSource}</td>
                                <td style={{whiteSpace: 'nowrap'}}>{record.permitNumber}</td>
                                <td style={{
                                    maxWidth: '300px', overflow: 'hidden',
                                    textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                                }}>
                                    {record.originalFileName}
                                </td>
                                <td>{record.template}</td>
                                <td>{record.completeness}%</td>
                                <td>{done}/{record.sections.length}</td>
                                {SHOW_RULES_VERSION && (
                                    <td style={{color: staleRules(record) ? '#b8860b' : '#2e7d32'}}>
                                        {staleRules(record) ? 'stale' : 'current'}
                                    </td>
                                )}
                                <td style={{color: STATE_COLOURS[record.state], fontWeight: 600}}>{record.state}</td>
                                <td style={{whiteSpace: 'nowrap'}}>
                                    <button onClick={() => openRecordById(record.fileId)}>
                                        {disagrees(record) ? 'Review' : 'Verify'}
                                    </button>
                                    {SHOW_RE_RUN_CONTROLS && (
                                        <button
                                            onClick={() => setReRunQueue(current =>
                                                current.includes(record.fileId)
                                                    ? current
                                                    : [...current, record.fileId])}
                                            title="Queue this one file for re-extraction">
                                            Re-run
                                        </button>
                                    )}
                                    <button
                                        onClick={() => setLakePreviewFileId(record.fileId)}
                                        disabled={record.state !== 'Verified'}
                                        title={record.state === 'Verified'
                                            ? 'Show the record as it would reach the lake'
                                            : 'Every section must be confirmed, corrected or marked none first'}>
                                        JSON
                                    </button>
                                </td>
                            </tr>
                        );
                    })}
                    {visible.length === 0 && (
                        <tr><td colSpan={SHOW_RULES_VERSION ? 10 : 9}><em>Nothing in this queue.</em></td></tr>
                    )}
                </tbody>
            </table>

            {openRecord && (
                <InspectionReportVerificationModal
                    record={openRecord}
                    onVerifySection={(sectionName, verificationType, override) =>
                        verifySection(openRecord.fileId, sectionName, verificationType, override)}
                    form={openForm}
                    formLoading={openFormLoading}
                    formError={openFormError}
                    onFlagRecord={() => flagRecord(openRecord.fileId)}
                    onNextRecord={openNextInQueue}
                    onClose={() => openRecordById(null)}
                />
            )}

            {lakeRecord && (
                <div style={{
                    position: 'fixed', inset: '60px', background: '#fff', border: '1px solid #999',
                    boxShadow: '0 4px 24px rgba(0,0,0,0.3)', zIndex: 1000, padding: '10px',
                    overflow: 'auto',
                }}>
                    <button style={{float: 'right'}} onClick={() => setLakePreviewFileId(null)}>Close</button>
                    <h3 style={{marginTop: 0}}>Lake record</h3>
                    <JsonView src={buildLakeRecord(lakeRecord)} collapsed={2}/>
                </div>
            )}
        </div>
    );
}

export default InspectionReportVerificationPage;
