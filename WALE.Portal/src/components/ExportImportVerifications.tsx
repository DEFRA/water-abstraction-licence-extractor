import {useCallback, useEffect, useState} from 'react';
import { waleApiBaseUrl } from '../api/apiClient.ts';
import { ProcessRun } from '../api/generated/apiClient.ts'
import {
    getProcessRuns
} from '.././utils/dropDownUtils';
export function ExportImportVerifications() {

    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [isStarting, setIsStarting] = useState(false);
    const [uploading, setUploading] = useState(false);
    const [uploadProgress, setUploadProgress] = useState({current: 0, total: 0, currentChunk: 0, totalChunks: 0});
    const [failedUploads, setFailedUploads] = useState<{ filename: string; error: string }[]>([]);
    const [targetProcessRunId, setTargetProcessRunId] = useState<number | undefined>();
    const canUpload = targetProcessRunId !== undefined;
    const [processRuns, setProcessRuns] = useState<ProcessRun[]>([]);

    const uploadFileAsync = async (
        file: File,
        idx: number,
        failed: { filename: string; error: string }[],
        targetProcessRunId: number
    ) => {
        const data = new FormData();
        data.append("file", file);

        try {
            const response = await fetch(
                `${waleApiBaseUrl}/BFF/Verification/ImportCsv?ProcessRunId=${targetProcessRunId}`,
                {
                    method: "PUT",
                    body: data
                }
            );

            if (!response.ok) {
                throw new Error(await response.text());
            }
        } catch (error) {
            failed.push({
                filename: file.name,
                error: error instanceof Error
                    ? error.message
                    : "Upload failed"
            });
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

        if (filesToUpload.length === 0) {
            return;
        }

        setUploading(true);
        setSuccessMessage(null);
        setFailedUploads([]);

        setUploadProgress({
            current: 0,
            total: filesToUpload.length,
            currentChunk: 0,
            totalChunks: 0
        });

        const failed: { filename: string; error: string }[] = [];

        const maxConcurrentScrapers = 5;
        let uploadTasks: Promise<void>[] = [];

        for (let idx = 0; idx < filesToUpload.length; idx++) {
            const file = filesToUpload[idx];

            uploadTasks.push(
                uploadFileAsync(
                    file,
                    idx,
                    failed,
                    targetProcessRunId
                )
            );

            if (uploadTasks.length === maxConcurrentScrapers) {
                await Promise.all(uploadTasks);
                uploadTasks = [];
            }
        }

        if (uploadTasks.length > 0) {
            await Promise.all(uploadTasks);
        }

        setUploading(false);
        setFailedUploads(failed);

        const successfulCount =
            filesToUpload.length - failed.length;

        if (successfulCount > 0) {
            setSuccessMessage(
                `Uploaded ${successfulCount} ${
                    successfulCount === 1 ? "file" : "files"
                } successfully`
            );
        }

    }, [targetProcessRunId]);

    const startExportProcess = async () => {
        setError(null);
        setSuccessMessage(null);
        setIsStarting(true);

        try {
            const response = await fetch(
                `${waleApiBaseUrl}/BFF/Verification/ExtractHistory`,
                {
                    method: 'GET',
                    headers: {
                        Accept: 'text/csv'
                    }
                }
            );

            if (!response.ok) {
                throw new Error(`Export failed: ${response.statusText}`);
            }

            const blob = await response.blob();

            const contentDisposition =
                response.headers.get('content-disposition');

            let fileName = 'verifications.csv';

            const fileNameMatch =
                contentDisposition?.match(
                    /fileName\*?=(?:UTF-8'')?"?([^";]+)"?/i
                );

            if (fileNameMatch?.[1]) {
                fileName = decodeURIComponent(fileNameMatch[1]);
            }

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

    if (error) {
        return (
            <div className="container error">
                <p>Error: {error}</p>
                <button onClick={() => setError(null)}>Clear</button>
            </div>
        );
    }
    useEffect(() => {
        Promise.all([
            getProcessRuns()
        ])
            .then(([processRunsResult]) => {
                setProcessRuns(processRunsResult);               
            })
            .catch(error => {
                console.error('Error loading dropdown values:', error);
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

                    <select
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
                            ? "Drop csv import file here"
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
                        Uploading {uploadProgress.current} of {uploadProgress.total} files...
                        {uploadProgress.totalChunks > 0 && (
                            <span style={{marginLeft: '10px', fontSize: '0.9em', color: '#555'}}>
                                (Part {uploadProgress.currentChunk} of {uploadProgress.totalChunks})
                            </span>
                        )}
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