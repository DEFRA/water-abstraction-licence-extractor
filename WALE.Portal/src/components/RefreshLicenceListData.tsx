
import { useEffect, useMemo, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { waleApiBaseUrl } from '../api/apiClient.ts';
import { Client } from '../api/generated/apiClient';

interface RefreshLicenceListDataProps {
    processRunId: number;
}

interface ProcessEvent {
    processRunId: number;
    status: string;
    message?: string;
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
    const [isConnected, setIsConnected] = useState(false);

    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const statusRef = useRef<ProcessStatus>('idle');

    const client = useMemo(
        () => new Client(waleApiBaseUrl),
        []
    );

    const updateStatus = (value: ProcessStatus) => {
        statusRef.current = value;
        setStatus(value);
    };

    useEffect(() => {
        if (!processRunId) return;

        let disposed = false;

        const hubUrl =
            `${waleApiBaseUrl}/notificationsHub/process`.replace("-api-", "-portal-");

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connectionRef.current = connection;

        connection.on('ProcessCompleted', (event: ProcessEvent) => {
            if (disposed || event.processRunId !== processRunId) return;

            updateStatus('completed');
            setError(null);
            setSuccessMessage(
                event.message ??
                `Refresh Licence List Data completed for process run ${processRunId}.`
            );
        });

        connection.on('ProcessFailed', (event: ProcessEvent) => {
            if (disposed || event.processRunId !== processRunId) return;

            updateStatus('failed');
            setSuccessMessage(null);
            setError(
                event.message ??
                `Refresh Licence List Data failed for process run ${processRunId}.`
            );
        });

        connection.onreconnecting(() => {
            if (!disposed) setIsConnected(false);
        });

        connection.onreconnected(async () => {
            if (disposed) return;

            try {
                await connection.invoke('JoinProcess', processRunId);
                if (!disposed) setIsConnected(true);

                // Optional: refresh current status from API here
                // to recover notifications missed during reconnection.
            } catch (err) {
                console.error('Failed to rejoin process:', err);
            }
        });

        connection.onclose(() => {
            if (!disposed) setIsConnected(false);
        });

        const startConnection = async () => {
            try {
                await connection.start();

                if (disposed) {
                    await connection.stop();
                    return;
                }

                await connection.invoke('JoinProcess', processRunId);

                if (!disposed) setIsConnected(true);
            } catch (err) {
                console.error('SignalR connection error:', err);

                if (!disposed) {
                    setIsConnected(false);
                }
            }
        };

        void startConnection();

        return () => {
            disposed = true;
            connectionRef.current = null;
            setIsConnected(false);
            void connection.stop();
        };
    }, [processRunId]);

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

                updateStatus('processing');

                setSuccessMessage(
                    `Refresh Licence List Data started successfully for process run ${processRunId}.`
                );
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

                <div style={{ marginTop: '10px', fontSize: '12px' }}>
                    SignalR: {isConnected ? 'Connected' : 'Disconnected'}
                </div>
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
                    <p style={{ margin: 0 }}>{successMessage}</p>

                    <button
                        type="button"
                        aria-label="Clear success message"
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
