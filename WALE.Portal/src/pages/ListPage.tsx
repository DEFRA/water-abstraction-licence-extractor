import {useSearchParams} from 'react-router-dom';
import {OutputListDataItem } from "../api/generated/apiClient.ts";
import {useState, useEffect, useCallback} from 'react'
import {waleApiClient} from '../api/apiClient';
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
import {FileIdMapProvider, useFileIdMap} from "../utils/useFileIdMap.tsx";

function ListPage() {
    const [searchParams] = useSearchParams();
    const processRunId = parseInt(searchParams.get('processRunId') ?? '0');

    return (
        <FileIdMapProvider processRunId={processRunId}>
            <ListPageContent processRunId={processRunId}/>
        </FileIdMapProvider>
    );
}

function ListPageContent({processRunId}: {processRunId: number}) {
    const {getLicenceNumber} = useFileIdMap();
    const [pageNumber, setPageNumber] = useState(1);
    const [query, setQuery] = useState<ProcessRunQuery>({
        searchTerm: '',
        skip: 0,
        take: 1000,
        issuer: '',
        limitsEmpty: undefined,
        aggregatesFilter: undefined,
        purposesEmpty: undefined,
        pointsEmpty: undefined,
        ocrScan: undefined,
        issueYear: undefined,
        meansFound: undefined,
        ShortLicenceSetId: '',
        linkedLicencesType: '',
        verificationType: undefined,
        sortAscending: undefined,
        sortField: '',
        licenceNumbers: []
    });
    const [outputList, setOutputList] = useState<OutputListDataItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [activeTab, setActiveTab] = useState<'licences' | 'licenceSets' | 'files' | 'actions'>('licences');

    const [showSingles, setShowSingles] = useState(false);
    const [verifyType, setVerifyType] = useState('');
    

    const [licenceSetsTotals, setLicenceSetsTotals] = useState<LicenceSetsTotals | undefined>(undefined);

    const {
        filteredData,
    } = useFiltering(outputList);

    const totals = useTotals(filteredData, verifyType);

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

            let filterKey = processRunId +
                (currentQuery.searchTerm ?? '') +
                '' +
                currentQuery.skip +
                currentQuery.take +
                currentQuery.issuer +
                currentQuery.limitsEmpty +
                currentQuery.aggregatesFilter +
                currentQuery.ocrScan +
                currentQuery.purposesEmpty +
                currentQuery.pointsEmpty +
                currentQuery.issueYear +
                currentQuery.meansFound +
                currentQuery.ShortLicenceSetId +
                currentQuery.linkedLicencesType +
                currentQuery.verificationType +
                currentQuery.sortField +
                currentQuery.sortAscending +
                currentQuery.licenceNumbers;
            ;

            // @ts-ignore
            if (!force && filterKey == window.lastFilterKey) {
                return;
            }
            
            // @ts-ignore
            window.lastFilterKey = filterKey;
            
            setLoading(true);
            
            const listDataItems = await waleApiClient.getProcessRun(
                processRunId,
                currentQuery.searchTerm,
                '',
                currentQuery.skip,
                currentQuery.take,
                currentQuery.issuer,
                currentQuery.limitsEmpty,
                currentQuery.aggregatesFilter,
                currentQuery.ocrScan,
                currentQuery.purposesEmpty,
                currentQuery.pointsEmpty,
                currentQuery.issueYear,
                currentQuery.meansFound,
                currentQuery.ShortLicenceSetId,
                currentQuery.linkedLicencesType,
                currentQuery.verificationType,
                currentQuery.sortField,
                currentQuery.sortAscending,
                currentQuery.licenceNumbers
            );

            setOutputList(listDataItems.records);
            setVerifyType(currentQuery.verificationType ?? '');
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

    const openReportWithId = useCallback((fileId: string) => {
        const knownItem = outputList.find(item => item.fileId === fileId);
        openReport(fileId, processRunId, knownItem, openReportWithId);

        if (!knownItem) {
            const licenceNumber = getLicenceNumber(fileId);
            if (licenceNumber) {
                waleApiClient.getProcessRunList(
                    processRunId,
                    'N/A',
                    '',
                    0,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    undefined,
                    [licenceNumber]
                ).then(result => {
                    const resolvedItem = result.records?.find(item => item.fileId === fileId);
                    if (resolvedItem) {
                        updateModalOutputItem(fileId, resolvedItem);
                    }
                }).catch(err => {
                    console.error('Error resolving linked licence report data:', err);
                });
            }
        }
    }, [openReport, processRunId, outputList, getLicenceNumber, updateModalOutputItem]);

    const openLicenceSetReportWithId = useCallback((fileId: string, licenceSetId: string) => {
        openLicenceSetReport(fileId, licenceSetId, processRunId);
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
                            showSingles={showSingles}
                            onToggleSingles={setShowSingles}
                        />
                        </thead>
                        <tbody>
                        {filteredData.map((item, index) => (
                            <LicencesTableRow
                                item={item}
                                key={index}
                                oddRow={index % 2 === 0}
                                onOpenReport={openReportWithId}
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