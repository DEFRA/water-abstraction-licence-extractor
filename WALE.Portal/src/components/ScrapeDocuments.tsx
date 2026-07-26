import { useState } from 'react';
import { waleApiBaseUrl } from '../api/apiClient.ts';
import  { Client }from '../api/generated/apiClient';

export function ScrapeDocuments() {
    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [isStarting, setIsStarting] = useState(false);

    const client = new Client(waleApiBaseUrl);
    const startScrapingProcess = async () => {
        setError(null);
        setSuccessMessage(null);
        setIsStarting(true);

        try {
            await client.sendFileProcessOrchestrationMessage(undefined);

            setSuccessMessage('Scraping orchestration process started successfully.');
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Unknown error';
            setError(message);
        } finally {
            setIsStarting(false);
        }
    };

    if (error) {
        return (
            <div className="container error">
                <p>Error: {error}</p>
                <button onClick={() => setError(null)}>Clear</button>
            </div>
        );
    }

    return (
        <>
            <div
                style={{
                    border: '1px solid #c3e6cb',
                    color: '#155724',
                    padding: '10px',
                    marginBottom: '10px',
                    marginTop: '40px',
                    borderRadius: '4px',
                    position: 'relative'
                }}
            >
                <button
                    onClick={startScrapingProcess}
                    disabled={isStarting}
                    style={{
                        backgroundColor: '#dc3545',
                        color: 'white',
                        border: 'none',
                        padding: '5px 10px',
                        borderRadius: '4px',
                        cursor: isStarting ? 'not-allowed' : 'pointer',
                        marginTop: '10px'
                    }}
                >
                    {isStarting ? 'Starting...' : 'Start Orchestration Process'}
                </button>
            </div>

            {successMessage && (
                <div
                    style={{
                        backgroundColor: '#d4edda',
                        border: '1px solid #c3e6cb',
                        color: '#155724',
                        padding: '10px',
                        marginBottom: '10px',
                        borderRadius: '4px',
                        position: 'relative'
                    }}
                >
                    <p style={{ margin: 0 }}>{successMessage}</p>
                    <button
                        onClick={() => setSuccessMessage(null)}
                        style={{
                            position: 'absolute',
                            top: '5px',
                            right: '10px',
                            border: 'none',
                            background: 'transparent',
                            color: '#155724',
                            fontSize: '20px',
                            cursor: 'pointer',
                            fontWeight: 'bold'
                        }}
                    >
                        ×
                    </button>
                </div>
            )}
        </>
    );
}

export default ScrapeDocuments;