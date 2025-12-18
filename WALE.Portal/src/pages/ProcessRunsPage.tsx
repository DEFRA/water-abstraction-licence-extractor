import {ProcessRun} from "../api/generated/apiClient.ts";
import {useState, useEffect} from 'react'
import {waleApiClient} from '../api/apiClient';
import ProcessRunListItem from '../components/ProcessRunListItem.tsx';

function ProcessRunsPage() {
    const [processRuns, setProcessRuns] = useState<ProcessRun[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

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

    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <>
            {processRuns.length === 0
                ? (<p>No process runs found.</p>)
                : (
                    <ul className="process-runs-list">
                        {processRuns.map((run) => (
                            <ProcessRunListItem run={run} key={run.processRunId}/>
                        ))}
                    </ul>
                )}
        </>
    );
}

export default ProcessRunsPage;