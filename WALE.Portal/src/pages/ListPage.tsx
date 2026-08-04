import {useSearchParams} from 'react-router-dom';
import {OutputListDataItem } from "../api/generated/apiClient.ts";
import {useState, useEffect, useCallback} from 'react'
import {waleApiClient} from '../api/apiClient';
import LicencesTableHeaders from "../components/LicencesTableHeaders";
import LicencesTableRow from "../components/LicencesTableRow";
import LicencesTableFooters from "../components/LicencesTableFooters";
import LicenceSetsTableHeaders from "../components/LicenceSetsTableHeaders";
import LicenceSetsTableFooters from "../components/LicenceSetsTableFooters";
import LicenceSetsTableBody, {type LicenceSetsTotals} from "../components/LicenceSetsTableBody";
import FilesList from "../components/FilesList";
import '../assets/liststyles.css'
import {useFiltering} from "../utils/useFiltering.ts";
import {useTotals} from "../utils/useTotals.ts";
import {useReportModals} from "../utils/useReportModals.ts";
import {ReportModalContainer} from "../components/ReportModalContainer";
import Paging from "../components/Paging.tsx";
import type {ProcessRunQuery} from "../class/ProcessRunQuery.tsx";
import ProcessRunLicenceFilters from "../components/ProcessRunLicenceFilters";
import ScrapeDocuments from "../components/ScrapeDocuments.tsx";

function ListPage() {
    const [searchParams] = useSearchParams();
    const [pageNumber, setPageNumber] = useState(1);
    const [query, setQuery] = useState<ProcessRunQuery>({
        searchTerm: '',
        skip: 0,
        take: 1000,
        issuer: '',
        limitsEmpty: undefined,
        aggregatesEmpty: undefined,
        purposesEmpty: undefined,
        pointsEmpty: undefined,
        ocrScan: undefined,
        issueYear: undefined,
        meansFound: undefined,
        ShortLicenceSetId: '',
        linkedLicencesType: '',
        verificationType: undefined,
        sortAscending: undefined,
        sortField: ''
    });
    const processRunId = searchParams.get('processRunId');
    const [outputList, setOutputList] = useState<OutputListDataItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [activeTab, setActiveTab] = useState<'licences' | 'licenceSets' | 'files' | 'actions'>('licences');

    const [showSingles, setShowSingles] = useState(false);

    const [licenceSetsTotals, setLicenceSetsTotals] = useState<LicenceSetsTotals | undefined>(undefined);

    const {
        filteredData,
        applyFilter,
        resetFiltersExcept,
        toggleSort,
        filters
    } = useFiltering(outputList);

    const totals = useTotals(filteredData);

    const [totalPages, setTotalPages] = useState(1);
    const [totalLicences, setTotalLicences] = useState(0);
    const [issuers, setIssuers] = useState<string[]>([]);
    const [issueDates, setIssueDates] = useState<string[]>([]);
    const [shortLicenceIds, setShortLicenceIds] = useState<string[]>([]);
    const fetchOutputList = useCallback(async (force: boolean = false) => {
        try {
            const currentQuery: ProcessRunQuery = {
                ...query,
                searchTerm: query.searchTerm?.trim() || "N/A",
                skip: (pageNumber - 1) * query.take,
                take: query.take
            };

            let filterKey = parseInt(processRunId ?? '0') +
                (currentQuery.searchTerm ?? '') +
                '' +
                currentQuery.skip +
                currentQuery.take +
                currentQuery.issuer +
                currentQuery.limitsEmpty +
                currentQuery.aggregatesEmpty +
                currentQuery.ocrScan +
                currentQuery.purposesEmpty +
                currentQuery.pointsEmpty +
                currentQuery.issueYear +
                currentQuery.meansFound +
                currentQuery.ShortLicenceSetId +
                currentQuery.linkedLicencesType +
                currentQuery.verificationType;

            // @ts-ignore
            if (!force && filterKey == window.lastFilterKey) {
                return;
            }
            
            // @ts-ignore
            window.lastFilterKey = filterKey;
            
            setLoading(true);
            
            const listDataItems = await waleApiClient.getProcessRun(
                parseInt(processRunId ?? '0'),
                currentQuery.searchTerm,
                '',
                currentQuery.skip,
                currentQuery.take,
                currentQuery.issuer,
                currentQuery.limitsEmpty,
                currentQuery.aggregatesEmpty,
                currentQuery.ocrScan,
                currentQuery.purposesEmpty,
                currentQuery.pointsEmpty,
                currentQuery.issueYear,
                currentQuery.meansFound,
                currentQuery.ShortLicenceSetId,
                currentQuery.linkedLicencesType,
                currentQuery.verificationType,
                currentQuery.sortField,
                currentQuery.sortAscending
            );

            setOutputList(listDataItems.records);
            setShortLicenceIds(listDataItems.licenceSetIds ?? []);
            setIssuers(listDataItems.issuers ?? []);
            setIssueDates(listDataItems.issueDates ?? []);
            const totalRecords = listDataItems.totalRecords;
            setTotalLicences(totalRecords);
            setTotalPages(totalRecords > 0 ? Math.ceil(totalRecords / currentQuery.take) : 0);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to fetch process runs');
            console.error('Error fetching process runs:', err);
        } finally {
            setLoading(false);
        }
    }, [processRunId, pageNumber, query, totalLicences]);

    useEffect(() => {
        fetchOutputList();
    }, [fetchOutputList]);

    const {
        modals,
        openReport,
        openLicenceSetReport,
        closeModal,
        updateModalPosition,
        maximizeModal,
        minimizeModal,
        updateModalOutputItem
    } = useReportModals();

    const openReportWithId = useCallback((fileId: string, item?: OutputListDataItem) => {
        openReport(fileId, parseInt(processRunId ?? '0'), item, outputList, openReportWithId);
    }, [openReport, processRunId, outputList]);

    const openLicenceSetReportWithId = useCallback((fileId: string, licenceSetId: string) => {
        openLicenceSetReport(fileId, licenceSetId, parseInt(processRunId ?? '0'));
    }, [openLicenceSetReport, processRunId]);

    const updateQuery = <K extends keyof ProcessRunQuery>(
        key: K,
        value: ProcessRunQuery[K]
    ) => {
        setPageNumber(1);

        setQuery(previous => ({
            ...previous,
            [key]: value,
            skip: 0
        }));
    };

    useEffect(() => {
        modals.forEach(modal => {
            if (modal.type === 'report') {
                const updatedItem = outputList.find(item => item.fileId === modal.fileId);
                if (updatedItem && JSON.stringify(updatedItem) !== JSON.stringify(modal.outputListDataItem)) {
                    updateModalOutputItem(modal.fileId, updatedItem);
                }
            }
        });
    }, [outputList, modals, updateModalOutputItem]);
    
    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <div className="list-page-container">
            <h1>
                <a
                    href="#"
                    id="licencesLink"
                    className={activeTab === 'licences' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('licences');
                    }}>
                    Licences
                </a>
                {' | '}
                <a
                    href="#"
                    id="licenceSetsLink"
                    className={activeTab === 'licenceSets' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('licenceSets');
                    }}>
                    Licence sets
                </a>
                {' | '}
                <a
                    href="#"
                    id="filesLink"
                    className={activeTab === 'files' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('files');
                    }}>
                    Files
                </a>
                {' | '}
                <a
                    href="#"
                    id="actionsLink"
                    className={activeTab === 'actions' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('actions');
                    }}>
                    Actions
                </a>
            </h1>

            {activeTab === 'licences' && (
                <div id="licences">

                    <div style={{ clear: 'both', display: 'block', width: '100%', marginTop: '10px' }}>
                        <Paging
                            pageNumber={pageNumber}
                            totalPages={totalPages}
                            totalLicences={totalLicences}
                            pageSize={query.take}
                            searchTerm={query.searchTerm ?? ''}
                            setPageNumber={setPageNumber}
                            setPageSize={(value) => {
                                setPageNumber(1);
                                setQuery(previous => ({
                                    ...previous,
                                    take: value,
                                    skip: 0
                                }));
                            }}
                            setSearchTerm={(value) => updateQuery('searchTerm', value)}
                        />
                    </div>
                    <table id="licencesTable">
                        <thead>
                        <ProcessRunLicenceFilters
                            query={query}
                            setQuery={setQuery}
                            issuers={issuers}
                            shortLicenceIds={shortLicenceIds}
                            issueDates={issueDates}
                            setPageNumber={setPageNumber}
                        />
                        <LicencesTableHeaders
                            data={outputList}
                            onFilterChange={applyFilter}
                            onResetFilters={resetFiltersExcept}
                            onToggleSort={toggleSort}
                            onToggleSingles={setShowSingles}
                            filters={filters}
                            showSingles={showSingles}
                        /></thead>
                        <tbody>
                        {filteredData.map((item, index) => (
                            <LicencesTableRow
                                item={item} 
                                key={index} 
                                data={filteredData} 
                                oddRow={index % 2 === 0}
                                onOpenReport={(fileId) => openReportWithId(fileId, item)}
                                onOpenLicenceSetReport={openLicenceSetReportWithId}
                                showSingles={showSingles}
                            />
                        ))}
                        </tbody>
                        <tfoot><LicencesTableFooters totals={totals}/></tfoot>
                    </table>
                </div>
            )}

            {activeTab === 'licenceSets' && (
                <div id="licenceSets">
                    <table>
                        <thead><LicenceSetsTableHeaders/></thead>
                        <LicenceSetsTableBody 
                            data={filteredData} 
                            onOpenReport={openReportWithId} 
                            onOpenLicenceSetReport={openLicenceSetReportWithId} 
                            onTotalsCalculated={setLicenceSetsTotals}
                        />
                        <tfoot><LicenceSetsTableFooters totals={licenceSetsTotals}/></tfoot>
                    </table>
                    <p style={{fontStyle: 'italic'}}>NOTE - Only showing licence sets containing multiple licences</p>
                </div>
            )}

            {activeTab === 'files' && (
                <div id="files">
                    <FilesList/>
                </div>
            )}

            {activeTab === 'actions' && (
                <div id="actions">
                    <ScrapeDocuments/>
                </div>
            )}

            <ReportModalContainer
                modals={modals}
                onClose={closeModal}
                onMaximize={maximizeModal}
                onMinimize={minimizeModal}
                onPositionChange={updateModalPosition}
                onRefresh={() => fetchOutputList(true)}
                /*onOpenLinkedLicence={openReportWithId}*/
            />
        </div>);
}

export default ListPage;