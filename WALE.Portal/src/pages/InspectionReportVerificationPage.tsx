import {Fragment, useEffect, useMemo, useState} from 'react';
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

/// Caps the DOM the way MAX_ROWS_RENDERED does, counted in groups because a group is the unit a
/// reviewer reads here.
const MAX_GROUPS_RENDERED = 100;

/// Two documents in a group whose names match once the extension, case and punctuation are taken
/// off are the same report filed twice - "H Hey & Sons  WR51.pdf" against "H Hey & Sons WR51.pdf"
/// differs only by a double space, and a .doc against its own .pdf is the same document in two
/// formats. Names that all differ are the common case and usually mean several abstraction points
/// inspected on one day, so only the collision is worth marking.
function hasRepeatedName(group: InspectionRecord[]) {
    const stems = new Set(group.map(record => (record.originalFileName ?? '')
        .replace(/\.(pdf|docx?)$/i, '')
        .toLowerCase()
        .replace(/[^a-z0-9]/g, '')));

    return stems.size < group.length;
}

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

/// Sits alongside the queues rather than among them: a record with siblings still belongs in
/// whichever queue its own state puts it in, and removing it from there to put it here would hide
/// it from the reviewer working that queue.
const MultipleDocumentsView = 'multiple';

type ViewKey = QueueKey | typeof MultipleDocumentsView;

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
    const [chosenQueue, setChosenQueue] = useState<ViewKey | null>(null);
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

    const activeQueue: ViewKey = chosenQueue ?? defaultQueue;

    /// Inspections carrying more than one document, keyed on permit and the extracted inspection
    /// date. Deliberately not called duplicates: six documents can share a licence and a day and be
    /// six different abstraction points - permit 1041122001 on 2025-11-20 is Ten Acre Ghyll, The
    /// Pound, Burnt Barn, Henleys Bridge, The Park and Towerhouse, all legitimate. Telling those
    /// apart needs the site address, which the list query does not carry, so this shows the group
    /// and leaves the judgement to whoever reads the names.
    ///
    /// Keyed on the extracted date rather than sortDate, which falls back to DMS dates and would
    /// group documents that merely share an upload day.
    const documentGroups = useMemo(() => {
        const byKey = new Map<string, InspectionRecord[]>();

        for (const record of filtered) {
            if (!record.permitNumber || !record.inspectionDate) {
                continue;
            }

            const key = `${record.permitNumber.toLowerCase()}|${record.inspectionDate.slice(0, 10)}`;
            const group = byKey.get(key);

            if (group) {
                group.push(record);
            } else {
                byKey.set(key, [record]);
            }
        }

        return [...byKey.values()]
            .filter(group => group.length > 1)
            // Repeated names first: those are the groups a reviewer can act on without needing the
            // site address to tell the documents apart.
            .sort((left, right) =>
                Number(hasRepeatedName(right)) - Number(hasRepeatedName(left))
                || right.length - left.length
                || (right[0].inspectionDate ?? '').localeCompare(left[0].inspectionDate ?? ''));
    }, [filtered]);

    const groupedFileCount = documentGroups.reduce((total, group) => total + group.length, 0);

    const visible = useMemo(
        () => [...(bucketed.get(activeQueue as QueueKey) ?? [])].sort((left, right) =>
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
                {[...ACTIVE_QUEUES,
                  {key: 'verified' as ViewKey, label: 'Verified'},
                  {key: MultipleDocumentsView as ViewKey, label: 'Multiple documents'}].map(queue => {
                    const count = queue.key === MultipleDocumentsView
                        ? documentGroups.length
                        : bucketed.get(queue.key as QueueKey)?.length ?? 0;
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

            {activeQueue === MultipleDocumentsView ? (
                <>
                    <div style={{marginBottom: '6px', fontSize: '12px'}}>
                        {documentGroups.length} inspection(s) with more than one document,
                        {' '}{groupedFileCount} file(s) in total
                        {' '}&nbsp;|&nbsp; {documentGroups.filter(hasRepeatedName).length} with a
                        {' '}repeated name
                        {documentGroups.length > MAX_GROUPS_RENDERED
                            && <> (showing the first {MAX_GROUPS_RENDERED})</>}
                    </div>

                    <div style={{
                        background: '#eef4fb', border: '1px solid #b6cbe4', borderRadius: '3px',
                        padding: '6px 10px', margin: '0 0 10px', fontSize: '12px',
                    }}>
                        Grouped on permit number and extracted inspection date. A group is not
                        necessarily a duplicate - one licence can have several abstraction points
                        inspected on the same day, which produces a genuine report each. Compare the
                        document names before treating any of them as redundant.
                    </div>

                    <table className="licences-table" style={{width: '100%', fontSize: '12px'}}>
                        <thead>
                            <tr>
                                <th>Inspection</th>
                                <th>Permit</th>
                                <th>DMS name</th>
                                <th>Tmpl</th>
                                <th>Compl</th>
                                <th>State</th>
                                <th/>
                            </tr>
                        </thead>
                        <tbody>
                            {documentGroups.slice(0, MAX_GROUPS_RENDERED).map(group => (
                                <Fragment key={`${group[0].permitNumber}|${group[0].inspectionDate}`}>
                                    <tr style={{background: hasRepeatedName(group) ? '#fdecea' : '#e8eef5'}}>
                                        <td colSpan={7} style={{fontWeight: 600}}>
                                            {group[0].permitNumber} &nbsp;|&nbsp;
                                            {' '}{group[0].inspectionDate?.slice(0, 10)} &nbsp;|&nbsp;
                                            {' '}{group.length} documents
                                            {hasRepeatedName(group) && (
                                                <span style={{color: '#c62828', fontWeight: 400}}>
                                                    {' '}&mdash; two documents share a name, so one is
                                                    probably redundant
                                                </span>
                                            )}
                                        </td>
                                    </tr>
                                    {group.map(record => (
                                        <tr key={record.fileId}>
                                            <td>{record.inspectionDate?.slice(0, 10)}</td>
                                            <td>{record.permitNumber}</td>
                                            <td style={{
                                                maxWidth: '380px', overflow: 'hidden',
                                                textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                                            }}>
                                                {record.originalFileName}
                                            </td>
                                            <td>{record.template}</td>
                                            <td>{record.completeness}%</td>
                                            <td style={{color: STATE_COLOURS[record.state], fontWeight: 600}}>
                                                {record.state}
                                            </td>
                                            <td>
                                                <button onClick={() => openRecordById(record.fileId)}>
                                                    Verify
                                                </button>
                                            </td>
                                        </tr>
                                    ))}
                                </Fragment>
                            ))}
                            {documentGroups.length === 0 && (
                                <tr><td colSpan={7}><em>No inspection has more than one document.</em></td></tr>
                            )}
                        </tbody>
                    </table>
                </>
            ) : (
            <>
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
            </>
            )}

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
