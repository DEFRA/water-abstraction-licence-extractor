import {ProcessRun} from "../api/generated/apiClient.ts";
import {useState, useEffect, useMemo} from 'react'
import {waleApiClient} from '../api/apiClient';
import ProcessRunListItem from '../components/ProcessRunListItem.tsx';

const DOCUMENT_TYPES: { key: string; label: string }[] = [
    {key: 'AbstractionLicence', label: 'Abstraction Licence'},
    {key: 'WrInspectionReport', label: 'Inspection Report'},
];

function ProcessRunsPage() {
    const [processRuns, setProcessRuns] = useState<ProcessRun[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [activeTab, setActiveTab] = useState(DOCUMENT_TYPES[0].key);

    useEffect(() => {
        const fetchProcessRuns = async () => {
            try {
                const runs = await waleApiClient.getProcessRuns();
                setProcessRuns(runs);
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to fetch process runs');
                console.error('Error fetching process runs:', err);
            } finally {
                setLoading(false);
            }
        };

        fetchProcessRuns();
    }, []);

    const countByType = useMemo(() => {
        const counts: Record<string, number> = {};
        for (const run of processRuns) {
            const documentType = (run as unknown as { documentType?: string }).documentType ?? 'AbstractionLicence';
            counts[documentType] = (counts[documentType] ?? 0) + 1;
        }
        return counts;
    }, [processRuns]);

    const visibleRuns = useMemo(
        () => processRuns.filter(run =>
            ((run as unknown as { documentType?: string }).documentType ?? 'AbstractionLicence') === activeTab
        ),
        [processRuns, activeTab]
    );

    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <>
            <h1>
                {DOCUMENT_TYPES.map((type, index) => (
                    <span key={type.key}>
                        {index > 0 && ' | '}
                        <a
                            href="#"
                            className={activeTab === type.key ? 'selected' : ''}
                            onClick={(e) => {
                                e.preventDefault();
                                setActiveTab(type.key);
                            }}>
                            {type.label} ({countByType[type.key] ?? 0})
                        </a>
                    </span>
                ))}
            </h1>

            {visibleRuns.length === 0
                ? (<p>No process runs found.</p>)
                : (
                    <ul className="process-runs-list">
                        {visibleRuns.map((run) => (
                            <ProcessRunListItem run={run} key={run.processRunId}/>
                        ))}
                    </ul>
                )}
        </>
    );
}

export default ProcessRunsPage;