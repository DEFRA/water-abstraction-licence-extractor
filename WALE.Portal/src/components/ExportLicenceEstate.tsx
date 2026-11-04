import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { waleApiBaseUrl } from '../api/apiClient.ts';

interface ExportLicenceEstateProps {
    processRunId: number;
}

interface ProcessEvent {
    processRunId: number;
    status: string;
    message?: string;
}

type ExportStatus = 'idle' | 'starting' | 'exporting' | 'completed' | 'failed';

export function ExportLicenceEstate({ processRunId }: ExportLicenceEstateProps) {
    const [status, setStatus] = useState<ExportStatus>('idle');
    const [message, setMessage] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [url, setUrl] = useState<string | null>(null);

    const statusRef = useRef<ExportStatus>('idle');

    const updateStatus = (value: ExportStatus) => {
        statusRef.current = value;
        setStatus(value);
    };

    const fetchUrl = async () => {
        const response = await fetch(
            `${waleApiBaseUrl}/Extractor/LicenceEstateExport/GetRunExportUrl`
            + `?processRunId=${processRunId}`);

        if (!response.ok) return null;

        const body = await response.json();

        return body.url as string;
    };

    useEffect(() => {
        if (!processRunId) return;

        let disposed = false;

        const hubUrl =
            `${waleApiBaseUrl}/notificationsHub/process`.replace("-api-", "-portal-");

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('LicenceEstateExportCompleted', async (event: ProcessEvent) => {
            if (disposed || event.processRunId !== processRunId) return;

            // Everyone on the page is in the group, so only the tab that asked shows a link.
            if (statusRef.current !== 'starting' && statusRef.current !== 'exporting') return;

            updateStatus('completed');
            setError(null);
            setMessage(event.message ?? `Export completed for process run ${processRunId}.`);
            setUrl(await fetchUrl());
        });

        connection.on('LicenceEstateExportFailed', (event: ProcessEvent) => {
            if (disposed || event.processRunId !== processRunId) return;
            if (statusRef.current !== 'starting' && statusRef.current !== 'exporting') return;

            updateStatus('failed');
            setMessage(null);
            setError(event.message ?? `Export failed for process run ${processRunId}.`);
        });

        const start = async () => {
            try {
                await connection.start();

                if (disposed) {
                    await connection.stop();
                    return;
                }

                await connection.invoke('JoinProcess', processRunId);
            } catch (err) {
                console.error('SignalR connection error:', err);
            }
        };

        void start();

        return () => {
            disposed = true;
            void connection.stop();
        };
    }, [processRunId]);

    const startExport = async () => {
        if (!processRunId || statusRef.current === 'starting'
            || statusRef.current === 'exporting') {
            return;
        }

        setError(null);
        setMessage(null);
        setUrl(null);
        updateStatus('starting');

        try {
            const response = await fetch(
                `${waleApiBaseUrl}/Extractor/LicenceEstateExport/StartRunExport`
                + `?processRunId=${processRunId}`,
                { method: 'POST' });

            if (!response.ok) throw new Error(`The API returned ${response.status}.`);

            if (statusRef.current === 'starting') {
                updateStatus('exporting');
                setMessage(`Exporting process run ${processRunId}. This takes a few minutes.`);
            }
        } catch (err) {
            if (statusRef.current === 'completed') return;

            updateStatus('failed');
            setError(err instanceof Error ? err.message : 'Unknown error');
        }
    };

    const isRunning = status === 'starting' || status === 'exporting';

    return (
        <div style={{ marginTop: '20px' }}>
            <button
                type="button"
                onClick={startExport}
                disabled={isRunning}
                style={{
                    backgroundColor: '#0b6623',
                    color: 'white',
                    border: 'none',
                    padding: '5px 10px',
                    borderRadius: '4px',
                    cursor: isRunning ? 'not-allowed' : 'pointer'
                }}
            >
                {isRunning ? 'Exporting...' : 'Export licences as JSON'}
            </button>

            {message && <p style={{ margin: '10px 0 0', fontSize: '13px' }}>{message}</p>}

            {url && (
                <p style={{ margin: '10px 0 0' }}>
                    <a href={url}>Download the zip</a> (the link expires in an hour)
                </p>
            )}

            {error && (
                <p style={{ margin: '10px 0 0', color: '#721c24', fontSize: '13px' }}>
                    Error: {error}
                </p>
            )}
        </div>
    );
}

export default ExportLicenceEstate;
