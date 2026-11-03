import {useState, useEffect, useCallback, useMemo, Fragment} from 'react';
import {useSearchParams} from 'react-router-dom';
import {OutputListDataItem, LicenceRunHistoryItem} from "../api/generated/apiClient.ts";
import {waleApiClient} from "../api/apiClient";
import LicenceRunHistory from "../components/LicenceRunHistory";
import LicenceLookupPanel from "../components/LicenceLookupPanel";
import {getLicenceEstate} from "../api/licenceEstate";
import LicencesTableRow from "../components/LicencesTableRow";
import ProcessRunLicenceFilters from "../components/ProcessRunLicenceFilters";
import Paging from "../components/Paging.tsx";
import {useReportModals} from "../utils/useReportModals.ts";
import {ReportModalContainer} from "../components/ReportModalContainer";
import {LICENCE_QUEUES} from "../utils/licenceQueues";
import type {LicenceEstateQuery} from "../class/LicenceEstateQuery.tsx";
import {queryFromParams, paramsFromQuery} from "../utils/licenceEstateParams";
import '../assets/liststyles.css'

/// LicencesTableRow's cell count, which the expanded row has to span.
const LicenceRowColumns = 14;

export default function LicencesPage() {
    const [searchParams, setSearchParams] = useSearchParams();
    const query = useMemo(() => queryFromParams(searchParams), [searchParams]);

    /// Writes to the URL but typed as a React setter, so ProcessRunLicenceFilters takes it
    /// in place of a useState one.
    const setQuery: React.Dispatch<React.SetStateAction<LicenceEstateQuery>> = (action) => {
        const next = typeof action === 'function' ? action(query) : action;

        setSearchParams(paramsFromQuery(next), {replace: true});
    };

    /// The licenceNumbers filter already narrows the table, so one entry doubles as the signal
    /// to open the lookup - no separate parameter needed.
    const lookedUpLicence = query.licenceNumbers?.length === 1
        ? query.licenceNumbers[0]
        : undefined;

    const pageNumber = Math.floor(query.skip / query.take) + 1;
    const setPageNumber = (value: number) =>
        setQuery({...query, skip: (value - 1) * query.take});

    const [records, setRecords] = useState<OutputListDataItem[]>([]);
    const [queueCounts, setQueueCounts] = useState<Record<string, number>>({});
    const [totalLicences, setTotalLicences] = useState(0);
    const [issuers, setIssuers] = useState<string[]>([]);
    const [shortLicenceIds, setShortLicenceIds] = useState<string[]>([]);
    const [issueDates, setIssueDates] = useState<string[]>([]);
    const [statuses, setStatuses] = useState<string[]>([]);
    const [processRunIds, setProcessRunIds] = useState<number[]>([]);
    const [showSingles, setShowSingles] = useState(true);

    /// Bumped to re-run the fetch after a verification is written, since the URL has not changed.
    const [refreshes, setRefreshes] = useState(0);

    /// The twelve column filters are advanced: the queue bar, the three selects above and the
    /// search box cover the common cases, so they start out of the way.
    const [showColumnFilters, setShowColumnFilters] = useState(false);

    const [expanded, setExpanded] = useState<string[]>([]);

    /// Kept across collapses but dropped on refresh: a verification changes a run's queue.
    const [runHistory, setRunHistory] = useState<Record<string, LicenceRunHistoryItem[]>>({});
    const [error, setError] = useState<string | null>(null);

    const [loadedQuery, setLoadedQuery] = useState<LicenceEstateQuery | null>(null);
    const loading = loadedQuery !== query;

    useEffect(() => {
        let cancelled = false;

        getLicenceEstate(query)
            .then(response => {
                if (cancelled) {
                    return;
                }

                setRecords(response.records ?? []);
                setQueueCounts(response.queueCounts ?? {});
                setTotalLicences(response.totalRecords ?? 0);
                setIssuers(response.issuers ?? []);
                setShortLicenceIds(response.licenceSetIds ?? []);
                setIssueDates(response.issueDates ?? []);
                setStatuses(response.statuses ?? []);
                setProcessRunIds(response.processRunIds ?? []);
                setError(null);
                setLoadedQuery(query);
            })
            .catch(err => {
                if (!cancelled) {
                    setError(err instanceof Error ? err.message : 'Failed to fetch the licence estate');
                    setLoadedQuery(query);
                }
            });

        return () => {
            cancelled = true;
        };
    }, [query, refreshes]);

    const {
        modals,
        openReport,
        openLicenceSetReport,
        closeModal,
        updateModalPosition,
        maximizeModal,
        minimizeModal
    } = useReportModals();

    /// Each row carries the run its own extraction came from; the page has no single run.
    const runOf = useCallback(
        (fileId: string) => records.find(record => record.fileId === fileId)?.processRunId ?? 0,
        [records]);

    function openReportWithId(fileId: string, licenceId: number, matchesResultId: number) {
        const row = records.find(record => record.fileId === fileId);

        openReport(fileId, licenceId, matchesResultId, runOf(fileId), row, openReportWithId);
    }

    const openLicenceSetReportWithId = useCallback(
        (fileId: string, licenceId: number, matchesResultId: number, licenceSetId: string) =>
            openLicenceSetReport(fileId, licenceId, matchesResultId, licenceSetId, runOf(fileId)),
        [openLicenceSetReport, runOf]);

    const chooseQueue = (queue: number | undefined) =>
        setQuery({...query, queue, skip: 0});

    const toggleExpanded = (licenceNumber: string) => {
        if (expanded.includes(licenceNumber)) {
            setExpanded(expanded.filter(entry => entry !== licenceNumber));
            return;
        }

        setExpanded([...expanded, licenceNumber]);

        if (runHistory[licenceNumber] === undefined) {
            waleApiClient.getLicenceRunHistory(licenceNumber)
                .then(runs => setRunHistory(previous => ({...previous, [licenceNumber]: runs})))
                .catch(err => console.error('Error fetching run history:', err));
        }
    };

    const totalPages = totalLicences > 0 ? Math.ceil(totalLicences / query.take) : 0;

    return (
        <div>
            <div style={{display: 'flex', alignItems: 'baseline', gap: '16px', flexWrap: 'wrap'}}>
                <h1 style={{margin: 0}}>Licence estate</h1>

                {lookedUpLicence === undefined && <>
                <label>
                    Status{' '}
                    <select
                        value={query.projectedStatus ?? ''}
                        onChange={(e) => setQuery({
                            ...query,
                            projectedStatus: e.target.value === '' ? undefined : e.target.value,
                            skip: 0
                        })}>
                        <option value="">Any status</option>
                        {statuses.map(status => (
                            <option key={status} value={status}>{status}</option>
                        ))}
                    </select>
                </label>

                <label>
                    Run{' '}
                    <select
                        value={query.processRunId ?? ''}
                        onChange={(e) => setQuery({
                            ...query,
                            processRunId: e.target.value === '' ? undefined : Number(e.target.value),
                            skip: 0
                        })}>
                        <option value="">Newest per licence</option>
                        {processRunIds.map(run => (
                            <option key={run} value={run}>{run}</option>
                        ))}
                    </select>
                </label>

                <label>
                    Seen in{' '}
                    <select
                        value={query.maximumRunCount ?? ''}
                        onChange={(e) => setQuery({
                            ...query,
                            maximumRunCount: e.target.value === '' ? undefined : Number(e.target.value),
                            skip: 0
                        })}>
                        <option value="">Any number of runs</option>
                        <option value="1">One run only</option>
                        <option value="3">Three runs or fewer</option>
                        <option value="6">Six runs or fewer</option>
                    </select>
                </label>
                </>}
            </div>

            {lookedUpLicence === undefined &&
            <div style={{display: 'flex', gap: '6px', flexWrap: 'wrap', margin: '10px 0'}}>
                {[{queue: undefined, label: 'All licences'}, ...LICENCE_QUEUES].map(entry => {
                    const count = entry.queue === undefined
                        ? Object.values(queueCounts).reduce((total, value) => total + value, 0)
                        : queueCounts[String(entry.queue)] ?? 0;
                    const isActive = query.queue === entry.queue;

                    return (
                        <button
                            key={entry.label}
                            onClick={() => chooseQueue(entry.queue)}
                            style={{
                                padding: '5px 10px',
                                cursor: 'pointer',
                                border: isActive ? '2px solid #1565c0' : '1px solid #bbb',
                                background: isActive ? '#e3f2fd' : '#fafafa',
                                fontWeight: isActive ? 600 : 400,
                            }}>
                            {entry.label} ({count})
                        </button>
                    );
                })}

                <button
                    onClick={() => setQuery({
                        ...query,
                        minimumDocumentCount: query.minimumDocumentCount ? undefined : 2,
                        skip: 0
                    })}
                    style={{
                        padding: '5px 10px',
                        cursor: 'pointer',
                        border: query.minimumDocumentCount ? '2px solid #1565c0' : '1px solid #bbb',
                        background: query.minimumDocumentCount ? '#e3f2fd' : '#fafafa',
                        fontWeight: query.minimumDocumentCount ? 600 : 400,
                    }}>
                    Multiple documents
                </button>
            </div>}

            {lookedUpLicence !== undefined && (
                <LicenceLookupPanel
                    licenceNumber={lookedUpLicence}
                    onClear={() => setQuery({...query, licenceNumbers: [], skip: 0})}
                    onLookUp={(variant) => setQuery({...query, licenceNumbers: [variant], skip: 0})}
                />
            )}

            {error && <div style={{color: '#b00'}}>{error}</div>}
            {loading && <div>Loading…</div>}

            {!loading && !error && (
                <div>
                    {lookedUpLicence === undefined && <Paging
                        pageNumber={pageNumber}
                        totalPages={totalPages}
                        totalLicences={totalLicences}
                        pageSize={query.take}
                        searchTerm={query.searchTerm ?? ''}
                        setPageNumber={setPageNumber}
                        setPageSize={(value) => setQuery({...query, take: value, skip: 0})}
                        setSearchTerm={(value) => setQuery({...query, searchTerm: value, skip: 0})}
                        alignLeft
                    />}

                    {records.length === 0 && query.projectedStatus !== undefined && (
                        <div style={{margin: '8px 0', fontSize: '13px'}}>
                            Nothing matches with status <strong>{query.projectedStatus}</strong>.{' '}
                            <a href="#"
                               onClick={(e) => {
                                   e.preventDefault();
                                   setQuery({...query, projectedStatus: undefined, skip: 0});
                               }}>Search every status</a> — the licences worth finding are often
                            the ones NALD does not recognise.
                        </div>
                    )}

                    {lookedUpLicence === undefined && (
                        <button
                            onClick={() => setShowColumnFilters(previous => !previous)}
                            title="Per-column filters"
                            style={{cursor: 'pointer', fontSize: '11px', padding: '1px 6px'}}>
                            {showColumnFilters ? '\u25BE filters' : '\u25B8 filters'}
                        </button>
                    )}

                    <table id="licencesTable">
                        <thead>
                            <ProcessRunLicenceFilters
                                query={query}
                                setQuery={setQuery}
                                issuers={issuers}
                                shortLicenceIds={shortLicenceIds}
                                issueDates={issueDates}
                                setPageNumber={setPageNumber}
                                showSingles={showSingles}
                                onToggleSingles={setShowSingles}
                                hideFilterRow={lookedUpLicence !== undefined || !showColumnFilters}
                            />
                        </thead>
                        <tbody>
                            {records.map((item, index) => (
                                <Fragment key={`${item.fileId}-${item.licenceId}`}>
                                    <LicencesTableRow
                                        item={item}
                                        oddRow={index % 2 === 0}
                                        lookUpHref={`/licences?licenceNumber=${encodeURIComponent(item.licenceNumber ?? '')}`}
                                        onLookUp={() => setQuery({
                                            ...query,
                                            licenceNumbers: [item.licenceNumber ?? ''],
                                            skip: 0
                                        })}
                                        expanded={expanded.includes(item.licenceNumber ?? '')}
                                        onToggleExpand={() => toggleExpanded(item.licenceNumber ?? '')}
                                        onOpenReport={openReportWithId}
                                        onOpenLicenceSetReport={openLicenceSetReportWithId}
                                        showSingles={showSingles}
                                    />
                                    {expanded.includes(item.licenceNumber ?? '') && (
                                        <LicenceRunHistory
                                            runs={runHistory[item.licenceNumber ?? '']}
                                            columnCount={LicenceRowColumns}
                                        />
                                    )}
                                </Fragment>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <ReportModalContainer
                modals={modals}
                onClose={closeModal}
                onMaximize={maximizeModal}
                onMinimize={minimizeModal}
                onPositionChange={updateModalPosition}
                onRefresh={() => {
                    setRunHistory({});
                    setRefreshes(count => count + 1);
                }}
            />
        </div>
    );
}
