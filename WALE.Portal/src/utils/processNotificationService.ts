
import * as signalR from '@microsoft/signalr';
import { waleApiBaseUrl } from '../api/apiClient';

export interface ProcessEvent {
    processRunId: number;
    status: string;
    message?: string;
}

export type ProcessEventHandler = (event: ProcessEvent) => void;

class ProcessNotificationService {
    private connection: signalR.HubConnection;
    private startPromise: Promise<void> | null = null;

    // Lifecycle event subscribers
    private reconnectingHandlers = new Set<() => void>();
    private reconnectedHandlers = new Set<() => void>();
    private closedHandlers = new Set<() => void>();

    constructor() {
        const hubUrl =
            `${waleApiBaseUrl}/notificationsHub/process`
                .replace('-api-', '-portal-');

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Register SignalR lifecycle events once
        this.connection.onreconnecting(() => {
            this.reconnectingHandlers.forEach(handler => handler());
        });

        this.connection.onreconnected(() => {
            this.reconnectedHandlers.forEach(handler => handler());
        });

        this.connection.onclose(() => {
            this.closedHandlers.forEach(handler => handler());
        });
    }

    // Start the shared SignalR connection
    async start(): Promise<void> {
        if (this.startPromise) {
            return this.startPromise;
        }

        if (
            this.connection.state ===
            signalR.HubConnectionState.Connected
        ) {
            return;
        }

        if (
            this.connection.state !==
            signalR.HubConnectionState.Disconnected
        ) {
            return;
        }

        this.startPromise = this.connection.start();

        try {
            await this.startPromise;
        } finally {
            this.startPromise = null;
        }
    }

    // Subscribe to ProcessCompleted
    onCompleted(handler: ProcessEventHandler): () => void {
        this.connection.on('ProcessCompleted', handler);

        return () => {
            this.connection.off('ProcessCompleted', handler);
        };
    }

    // Subscribe to ProcessFailed
    onFailed(handler: ProcessEventHandler): () => void {
        this.connection.on('ProcessFailed', handler);

        return () => {
            this.connection.off('ProcessFailed', handler);
        };
    }

    // Check connection status
    get isConnected(): boolean {
        return (
            this.connection.state ===
            signalR.HubConnectionState.Connected
        );
    }

    // Subscribe to reconnecting
    onReconnecting(handler: () => void): () => void {
        this.reconnectingHandlers.add(handler);

        return () => {
            this.reconnectingHandlers.delete(handler);
        };
    }

    // Subscribe to reconnected
    onReconnected(handler: () => void): () => void {
        this.reconnectedHandlers.add(handler);

        return () => {
            this.reconnectedHandlers.delete(handler);
        };
    }

    // Subscribe to connection closed
    onClosed(handler: () => void): () => void {
        this.closedHandlers.add(handler);

        return () => {
            this.closedHandlers.delete(handler);
        };
    }

    // Explicitly stop the shared connection
    async stop(): Promise<void> {
        if (this.startPromise) {
            try {
                await this.startPromise;
            } catch {
                // Connection may already have failed
            }
        }

        await this.connection.stop();
    }
}

export const processNotificationService =
    new ProcessNotificationService();
