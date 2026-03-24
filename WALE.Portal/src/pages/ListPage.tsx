import {useSearchParams} from 'react-router-dom';
import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {useState, useEffect} from 'react'
import {waleApiClient} from '../api/apiClient';
import LicencesTableHeaders from "../components/LicencesTableHeaders";
import LicencesTableRow from "../components/LicencesTableRow";
import LicencesTableFooters from "../components/LicencesTableFooters";
import LicenceSetsTableHeaders from "../components/LicenceSetsTableHeaders";
import LicenceSetsTableFooters from "../components/LicenceSetsTableFooters";
import LicenceSetsTableBody, {type LicenceSetsTotals} from "../components/LicenceSetsTableBody";
import '../assets/liststyles.css'
import {useFiltering} from "../utils/useFiltering.ts";
import {useTotals} from "../utils/useTotals.ts";
import {useReportModals} from "../utils/useReportModals.ts";
import {ReportModalContainer} from "../components/ReportModalContainer";

function ListPage() {
    const [searchParams] = useSearchParams();
    const processRunId = searchParams.get('processRunId');

    const [outputList, setOutputList] = useState<OutputListDataItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [activeTab, setActiveTab] = useState<'licences' | 'licenceSets'>('licences');

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

    useEffect(() => {
        const fetchOutputList = async () => {
            try {
                const listDataItems = await waleApiClient.getProcessRun(parseInt(processRunId ?? '0'));
                setOutputList(listDataItems);
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to fetch process runs');
                console.error('Error fetching process runs:', err);
            } finally {
                setLoading(false);
            }
        };

        fetchOutputList();
    }, [processRunId]);

    const {
        modals,
        openReport,
        openLicenceSetReport,
        closeModal,
        updateModalPosition,
        maximizeModal,
        minimizeModal
    } = useReportModals();

    const openReportWithId = useCallback((filename: string) => {
        openReport(filename, parseInt(processRunId ?? '0'));
    }, [openReport, processRunId]);

    const openLicenceSetReportWithId = useCallback((filename: string, licenceSetId: string) => {
        openLicenceSetReport(filename, licenceSetId, parseInt(processRunId ?? '0'));
    }, [openLicenceSetReport, processRunId]);

    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <>
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
            </h1>

            {activeTab === 'licences' && (
                <div id="licences">
                    <table id="licencesTable">
                        <thead><LicencesTableHeaders
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

            <ReportModalContainer
                modals={modals}
                onClose={closeModal}
                onMaximize={maximizeModal}
                onMinimize={minimizeModal}
                onPositionChange={updateModalPosition}
                onOpenLinkedLicence={openReportWithId}
            />
        </>);
}

export default ListPage;