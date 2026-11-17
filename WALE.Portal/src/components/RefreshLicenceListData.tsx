
import { useMemo, useRef, useState } from 'react';
import { waleApiBaseUrl } from '../api/apiClient.ts';
import { Client } from '../api/generated/apiClient';

interface RefreshLicenceListDataProps {
    processRunId: number;
}

type ProcessStatus =
    | 'idle'
    | 'starting'
    | 'processing'
    | 'completed'
    | 'failed';

export function RefreshLicenceListData({
                                           processRunId
                                       }: RefreshLicenceListDataProps) {
    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [status, setStatus] = useState<ProcessStatus>('idle');

    const statusRef = useRef<ProcessStatus>('idle');

    const client = useMemo(
        () => new Client(waleApiBaseUrl),
        []
    );

    const updateStatus = (value: ProcessStatus) => {
        statusRef.current = value;
        setStatus(value);
    };
    

    const startRefreshProcess = async () => {
        if (!processRunId || statusRef.current === 'starting' ||
            statusRef.current === 'processing') {
            return;
        }

        setError(null);
        setSuccessMessage(null);

        updateStatus('starting');

        try {
            await client.updateLicenceListProcessRun(processRunId);

            // Completion may have arrived before the API returned.
            if (statusRef.current !== 'completed' &&
                statusRef.current !== 'failed') {
                setSuccessMessage(
                    `Refresh Licence List Data started successfully for process run ${processRunId}.`
                );
                updateStatus('completed');
            }
        } catch (err) {
            if (statusRef.current === 'completed') return;

            const message =
                err instanceof Error ? err.message : 'Unknown error';

            updateStatus('failed');
            setError(message);
        }
    };

    const isRunning =
        status === 'starting' || status === 'processing';

    return (
        <>
            <div
                style={{
                    border: '1px solid #c3e6cb',
                    color: '#155724',
                    padding: '10px',
                    marginBottom: '10px',
                    marginTop: '40px',
                    borderRadius: '4px'
                }}
            >
                <button
                    type="button"
                    onClick={startRefreshProcess}
                    disabled={isRunning}
                    style={{
                        backgroundColor: '#dc3545',
                        color: 'white',
                        border: 'none',
                        padding: '5px 10px',
                        borderRadius: '4px',
                        cursor: isRunning ? 'not-allowed' : 'pointer',
                        marginTop: '10px'
                    }}
                >
                    {status === 'starting'
                        ? 'Starting...'
                        : status === 'processing'
                            ? 'Processing...'
                            : 'Refresh Licence List Data'}
                </button>
                
            </div>

            {error && (
                <div
                    style={{
                        backgroundColor: '#f8d7da',
                        border: '1px solid #f5c6cb',
                        color: '#721c24',
                        padding: '10px',
                        marginBottom: '10px',
                        borderRadius: '4px'
                    }}
                >
                    <p style={{ margin: 0 }}>Error: {error}</p>
                    <button type="button" onClick={() => setError(null)}>
                        Clear
                    </button>
                </div>
            )}

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
                    <p style={{
                        margin: 0,
                        whiteSpace: 'pre-line',
                        paddingRight: '25px'
                    }}>
                        {successMessage}
                    </p>

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

export default RefreshLicenceListData;
