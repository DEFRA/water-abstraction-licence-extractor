import {useCallback, useEffect, useState} from 'react';
import { waleApiBaseUrl } from '../api/apiClient.ts';
import { ProcessRun } from '../api/generated/apiClient.ts'
import {
    getProcessRuns
} from '.././utils/dropDownUtils';
import {
    getVerificationDataStatus
} from '.././utils/verificationsDataUtils.tsx';
export function ExportImportVerifications() {

    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [isStarting, setIsStarting] = useState(false);
    const [uploading, setUploading] = useState(false);
    const [failedUploads, setFailedUploads] = useState<{ filename: string; error: string }[]>([]);
    const [targetProcessRunId, setTargetProcessRunId] = useState<number | undefined>();
    const canUpload = targetProcessRunId !== undefined;
    const [processRuns, setProcessRuns] = useState<ProcessRun[]>([]);
    const [currentBackupVersion, setCurrentBackupVersion] = useState<number | undefined>();
    const [currentBackupVerificationsCount, setCurrentBackupVerificationsCount] = useState<number | undefined>();
    const [currentVerificationsCount, setCurrentVerificationsCount] = useState<number | undefined>();
    const [latestBackupDate, setLatestBackupDate] = useState<string | undefined>();

    const uploadFileAsync = async (
        file: File,
        failed: { filename: string; error: string }[],
        targetProcessRunId: number
    ) => {
        const chunkSize = 500 * 1024; // 500 KB
        const totalChunks = Math.ceil(file.size / chunkSize);
        const uploadId = crypto.randomUUID();

        try {
            for (
                let chunkIndex = 0;
                chunkIndex < totalChunks;
                chunkIndex++
            ) {
                const start = chunkIndex * chunkSize;

                const end = Math.min(
                    start + chunkSize,
                    file.size
                );

                const chunk = file.slice(
                    start,
                    end
                );

                const data = new FormData();

                data.append(
                    "file",
                    chunk,
                    file.name
                );

                const response = await fetch(
                    `${waleApiBaseUrl}/BFF/Verification/ImportCsvChunk` +
                    `?processRunId=${targetProcessRunId}` +
                    `&uploadId=${uploadId}` +
                    `&chunkIndex=${chunkIndex}` +
                    `&totalChunks=${totalChunks}` +
                    `&fileName=${encodeURIComponent(file.name)}`,
                    {
                        method: "PUT",
                        body: data
                    }
                );

                if (!response.ok) {
                    throw new Error(
                        await response.text()
                    );
                }
            }
        } catch (error) {
            
            let message: string;
            message =  error instanceof Error
                ? error.message
                : "Upload failed"
            failed.push({
                filename: file.name,
                error:
                    error instanceof Error
                        ? error.message
                        : "Upload failed"
            });
            setError(message);
        }
    };
    
    
    const dropHandler = useCallback(async (
        event: React.DragEvent<HTMLDivElement>
    ) => {
        event.stopPropagation();
        event.preventDefault();

        if (!targetProcessRunId) {
            return;
        }

        const filesToUpload = event.dataTransfer.files;

        if (filesToUpload.length !== 1) {
            setError("Please upload one CSV file at a time.");
            return;
        }

        const file = filesToUpload[0];

        if (!file.name.toLowerCase().endsWith(".csv")) {
            setError("Please upload a CSV file.");
            return;
        }

        setUploading(true);
        setSuccessMessage(null);
        setFailedUploads([]);
        setError(null);

        const failed: { filename: string; error: string }[] = [];

        try {
            await uploadFileAsync(
                file,
                failed,
                targetProcessRunId
            );

            setFailedUploads(failed);

            if (failed.length === 0) {
                setSuccessMessage(
                    "Verification CSV file uploaded successfully"
                );

                await refreshVerificationData(true);
            }
        } finally {
            setUploading(false);
        }

    }, [targetProcessRunId]);

    const startExportProcess = async () => {
        setError(null);
        setSuccessMessage(null);
        setIsStarting(true);

        try {
            let chunk = 0;
            let hasMore = true;

            let fileName = 'verifications.csv';

            const csvParts: string[] = [];

            while (hasMore) {
                const response = await fetch(
                    `${waleApiBaseUrl}/BFF/Verification/ExtractHistory?chunk=${chunk}`
                );

                if (!response.ok) {
                    throw new Error(
                        `Export failed for chunk ${chunk}: ${response.statusText}`
                    );
                }

                const result = await response.json();

                if (chunk === 0) {
                    fileName = result.fileName;
                }

                let csv = result.csv;

                // Remove CSV header from subsequent chunks
                if (chunk > 0) {
                    const firstNewLineIndex =
                        csv.indexOf('\n');

                    if (firstNewLineIndex >= 0) {
                        csv =
                            csv.substring(
                                firstNewLineIndex + 1);
                    }
                }

                csvParts.push(csv);

                hasMore = result.hasMore;

                chunk++;
            }

            const blob = new Blob(
                csvParts,
                {
                    type: 'text/csv;charset=utf-8'
                }
            );

            const url = window.URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;

            document.body.appendChild(link);

            link.click();
            link.remove();

            window.URL.revokeObjectURL(url);

            setSuccessMessage(
                'Verification export downloaded successfully.'
            );
        } catch (err) {
            const message =
                err instanceof Error
                    ? err.message
                    : 'Unknown error';

            setError(message);
        } finally {
            setIsStarting(false);
        }
    };
    const refreshVerificationData = async (forceRefresh: boolean) => {
        const verificationDataStatusResult = await getVerificationDataStatus(forceRefresh);

        setCurrentVerificationsCount(
            verificationDataStatusResult.currentVerificationsCount
        );

        setCurrentBackupVerificationsCount(
            verificationDataStatusResult.currentVerificationsBackupCount
        );

        setCurrentBackupVersion(
            verificationDataStatusResult.currentVerificationsBackupVersion
        );
        
        setLatestBackupDate(
            verificationDataStatusResult.latestBackupVersionDate?.toLocaleString());
    };

    useEffect(() => {
        Promise.all([
            getProcessRuns(),
            refreshVerificationData(false)
        ])
            .then(([processRunsResult]) => {
                setProcessRuns(processRunsResult);
            })
            .catch(error => {
                console.error('Error loading utility values:', error);
            });
    }, []);
    
    return (
        <>
            
            <div className="container" style={{
                border: '1px solid #c3e6cb',
                color: '#155724',
                padding: '10px',
                marginBottom: '10px',
                marginTop: '40px',
                borderRadius: '4px',
                position: 'relative'
            }} >
                
                <p>Current Verification Count : {currentVerificationsCount} </p>
                <p>Current Backup Verification Count : {currentBackupVerificationsCount}</p>
                <p>Current Backup Version Number : {currentBackupVersion}</p>
                <p>Latest Import Date : {latestBackupDate}</p>
                
                <h3>Export Verifications Section</h3>
            <div style={{
                padding: '10px',
                marginBottom: '10px',
                borderRadius: '4px',
                position: 'relative'
            }} >
                <button
                    onClick={startExportProcess}
                    disabled={isStarting}
                    style={{
                        backgroundColor: '#dc3545',
                        color: 'white',
                        border: 'none',
                        padding: '5px 10px',
                        borderRadius: '4px',
                        cursor: isStarting ? 'not-allowed' : 'pointer'
                    }}
                >
                    {isStarting ? 'Starting...' : 'Start Verification Export Process'}
                </button>
            </div>
                {error && (
                    <div
                        style={{
                            backgroundColor: 'pink',
                            border: '1px solid #c3e6cb',
                            color: '#155724',
                            padding: '10px',
                            marginBottom: '10px',
                            borderRadius: '4px',
                            position: 'relative'
                        }}
                    >
                        <p style={{ margin: 0 }}>{error}</p>
                        <button
                            onClick={() => setError(null)}
                            style={{
                                position: 'absolute',
                                top: '5px',
                                right: '10px',
                                border: 'none',
                                background: 'transparent',
                                color: '#721c24',
                                fontSize: '20px',
                                cursor: 'pointer',
                                fontWeight: 'bold'
                            }}
                        >
                           X
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
                <h3>Import Verifications Section</h3>
                <div style={{ marginBottom: "12px" }}>
                    <label
                        htmlFor="targetProcessRunId"
                        style={{
                            display: "block",
                            marginBottom: "4px",
                            fontWeight: 600
                        }}
                    >
                        Target Process Run
                    </label>

                    <select style={{
                        width: '200px'
                    }}
                        id="targetProcessRunId"
                        value={targetProcessRunId ?? ""}
                        onChange={e =>
                            setTargetProcessRunId(
                                e.target.value === ""
                                    ? undefined
                                    : Number(e.target.value)
                            )
                        }
                    >
                        <option value="">Select target process run</option>

                        {processRuns?.map(processRun => (
                            <option
                                key={processRun.processRunId}
                                value={processRun.processRunId}
                            >
                                {processRun.processRunId}
                            </option>
                        ))}
                    </select>
                </div>

                <div
                    id="dragDropArea"
                    onDrop={canUpload ? dropHandler : undefined}
                    onDragOver={canUpload ? (e) => e.preventDefault() : undefined}
                    className={!canUpload ? "disabled" : ""}
                >
                    <p>
                        {canUpload
                            ? "Drop one verification CSV file here"
                            : "Select a target process run before uploading"}
                    </p>
                </div>
                
            {uploading && (
                <div style={{
                    backgroundColor: '#e7f3ff',
                    border: '1px solid #b3d7ff',
                    padding: '10px',
                    marginBottom: '10px',
                    borderRadius: '4px'
                }}>
                    <p style={{margin: 0}}>
                        Uploading verification csv...
                    </p>
                </div>
            )}
            
            {failedUploads.length > 0 && (
                <div style={{
                    backgroundColor: '#f8d7da',
                    border: '1px solid #f5c6cb',
                    color: '#721c24',
                    padding: '10px',
                    marginBottom: '10px',
                    borderRadius: '4px',
                    position: 'relative'
                }}>
                    <p style={{margin: 0}}>{failedUploads.length} {failedUploads.length === 1 ? 'file' : 'files'} failed
                        to upload after retries.</p>
                    <button
                        onClick={() => setFailedUploads([])}
                        style={{
                            position: 'absolute',
                            top: '5px',
                            right: '10px',
                            border: 'none',
                            background: 'transparent',
                            color: '#721c24',
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

export default ExportImportVerifications;