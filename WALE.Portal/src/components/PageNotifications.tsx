
import { useEffect, useState } from 'react';
import { processNotificationService } from '../utils/processNotificationService.ts'

interface PageNotificationsProps {
   
}

interface ProcessEvent {
    processRunId: number;
    status: string;
    message?: string;
}

export function PageNotifications({}: PageNotificationsProps) {
    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    
    useEffect(() => {
        let disposed = false;

        setIsConnected(processNotificationService.isConnected);

        const unsubscribeCompleted =
            processNotificationService.onCompleted((event: ProcessEvent) => {
                if (disposed) return;
                
                setError(null);

                setSuccessMessage(prev => {
                    const newMessage =
                        event.message ??
                        `Refresh Licence List Data succeeded for process run ${event.processRunId}.`

                    return prev
                        ? `${prev}\n${newMessage}`
                        : newMessage;
                });
            });

        const unsubscribeFailed =
            processNotificationService.onFailed((event: ProcessEvent) => {
                if (disposed) return;
                
                setSuccessMessage(null);
                setError(
                    event.message ??
                    `Refresh Licence List Data failed for process run ${event.processRunId}.`
                );
            });

        const unsubscribeReconnecting =
            processNotificationService.onReconnecting(() => {
                if (!disposed) setIsConnected(false);
            });

        const unsubscribeReconnected =
            processNotificationService.onReconnected(() => {
                if (!disposed) setIsConnected(true);
            });

        const unsubscribeClosed =
            processNotificationService.onClosed(() => {
                if (!disposed) setIsConnected(false);
            });

        void processNotificationService.start()
            .then(() => {
                if (!disposed) {
                    setIsConnected(processNotificationService.isConnected);
                }
            })
            .catch(err => {
                console.error('SignalR connection error:', err);
                if (!disposed) setIsConnected(false);
            });

        return () => {
            disposed = true;

            unsubscribeCompleted();
            unsubscribeFailed();
            unsubscribeReconnecting();
            unsubscribeReconnected();
            unsubscribeClosed();
        };
    }, []);

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
                <div style={{ marginTop: '10px', fontSize: '12px' }}>
                    SignalR: {isConnected ? 'Connected' : 'Disconnected'}
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

            </div>
        </>
    );
}

export default PageNotifications;
