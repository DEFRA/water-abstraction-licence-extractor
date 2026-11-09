
import { useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";

type ProcessStatus = "Completed" | "Failed" | null;

interface ProcessEvent {
    processRunId: number;
    status: string;
    completedAtUtc?: string;
}

export function useProcessNotification(
    processRunId: number | undefined,
    onCompleted?: () => void
) {
    const [status, setStatus] = useState<ProcessStatus>(null);

    useEffect(() => {
        if (processRunId == null) return;

        let disposed = false;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/process")
            .withAutomaticReconnect()
            .build();

        connection.on(
            "ProcessCompleted",
            (event: ProcessEvent) => {
                if (disposed || event.processRunId !== processRunId)
                    return;

                setStatus("Completed");
                onCompleted?.();
            }
        );

        connection.on(
            "ProcessFailed",
            (event: ProcessEvent) => {
                if (disposed || event.processRunId !== processRunId)
                    return;

                setStatus("Failed");
            }
        );

        const joinProcess = async () => {
            await connection.invoke("JoinProcess", processRunId);
        };

        connection.onreconnected(() => {
            void joinProcess().catch(console.error);
        });

        const start = async () => {
            try {
                await connection.start();

                if (disposed) {
                    await connection.stop();
                    return;
                }

                await joinProcess();
            } catch (error) {
                console.error("SignalR connection failed:", error);
            }
        };

        void start();

        return () => {
            disposed = true;
            void connection.stop();
        };
    }, [processRunId, onCompleted]);

    return status;
}
