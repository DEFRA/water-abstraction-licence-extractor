import {useState, useEffect, useCallback} from 'react';
import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {getLicenceEstate} from "../api/licenceEstate";
import LicencesTableRow from "../components/LicencesTableRow";
import ProcessRunLicenceFilters from "../components/ProcessRunLicenceFilters";
import Paging from "../components/Paging.tsx";
import {useReportModals} from "../utils/useReportModals.ts";
import {ReportModalContainer} from "../components/ReportModalContainer";
import type {LicenceEstateQuery} from "../class/LicenceEstateQuery.tsx";
import '../assets/liststyles.css'

/// One of Paging's own options, or its selector shows a size the page is not using.
const PageSize = 100;

export default function LicencesPage() {
    const [pageNumber, setPageNumber] = useState(1);
    const [query, setQuery] = useState<LicenceEstateQuery>({
        searchTerm: '',
        skip: 0,
        take: PageSize,
        issuer: '',
        ShortLicenceSetId: '',
        linkedLicencesType: '',
        sortField: '',
        sortAscending: true,
        licenceNumbers: []
    });

    const [records, setRecords] = useState<OutputListDataItem[]>([]);
    const [totalLicences, setTotalLicences] = useState(0);
    const [issuers, setIssuers] = useState<string[]>([]);
    const [shortLicenceIds, setShortLicenceIds] = useState<string[]>([]);
    const [issueDates, setIssueDates] = useState<string[]>([]);
    const [showSingles, setShowSingles] = useState(true);
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
                setTotalLicences(response.totalRecords ?? 0);
                setIssuers(response.issuers ?? []);
                setShortLicenceIds(response.licenceSetIds ?? []);
                setIssueDates(response.issueDates ?? []);
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
    }, [query]);

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

    const totalPages = totalLicences > 0 ? Math.ceil(totalLicences / query.take) : 0;

    return (
        <div>
            <h1>Licence estate</h1>

            {error && <div style={{color: '#b00'}}>{error}</div>}
            {loading && <div>Loading…</div>}

            {!loading && !error && (
                <div>
                    <Paging
                        pageNumber={pageNumber}
                        totalPages={totalPages}
                        totalLicences={totalLicences}
                        pageSize={query.take}
                        searchTerm={query.searchTerm ?? ''}
                        setPageNumber={(value) => {
                            setPageNumber(value);
                            setQuery(previous => ({...previous, skip: (value - 1) * previous.take}));
                        }}
                        setPageSize={(value) => {
                            setPageNumber(1);
                            setQuery(previous => ({...previous, take: value, skip: 0}));
                        }}
                        setSearchTerm={(value) => {
                            setPageNumber(1);
                            setQuery(previous => ({...previous, searchTerm: value, skip: 0}));
                        }}
                    />

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
                            />
                        </thead>
                        <tbody>
                            {records.map((item, index) => (
                                <LicencesTableRow
                                    item={item}
                                    key={`${item.fileId}-${item.licenceId}`}
                                    oddRow={index % 2 === 0}
                                    onOpenReport={openReportWithId}
                                    onOpenLicenceSetReport={openLicenceSetReportWithId}
                                    showSingles={showSingles}
                                />
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
                /// Same values, new identity: the fetch keys on the query object, so this
                /// re-runs it.
                onRefresh={() => setQuery(previous => ({...previous}))}
            />
        </div>
    );
}
