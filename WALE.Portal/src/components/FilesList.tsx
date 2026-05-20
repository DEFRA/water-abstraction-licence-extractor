import {useCallback, useEffect, useState} from 'react'
import {FilePdf} from "../class/FilePdf.tsx";
import {waleApiBaseUrl} from "../api/apiClient.ts";

interface FilesListProps {
}

export function FilesList({}: FilesListProps) {
    const [files, setFiles] = useState<FilePdf[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [uploading, setUploading] = useState(false);
    const [uploadProgress, setUploadProgress] = useState({current: 0, total: 0, currentChunk: 0, totalChunks: 0});
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [failedUploads, setFailedUploads] = useState<{ filename: string; error: string }[]>([]);

    useEffect(() => {
        fetchFiles();
    }, []);

    const fetchFiles = async () => {
        try {
            const files = await getFiles();
            setFiles(files);
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to fetch files');
            console.error('Error fetching files:', err);
        } finally {
            setLoading(false);
        }
    };

    const getFiles = async () => {
        let response = await fetch(waleApiBaseUrl + "/BFF/Files/ListAll");
        let items = await response.json();

        items = items.map(function (item: string | undefined) {
            let returnItem = new FilePdf();
            returnItem.filename = item;
            returnItem.permitNumber = extractPermitNumber(returnItem.filename);
            returnItem.fileId = extractFileId(returnItem.filename);

            return returnItem;
        });

        return items;
    };
    
    const extractPermitNumber = (fileName : string | undefined) => {
        if (fileName === null || fileName === undefined || fileName === "")
        {
            return "";
        }

        let underscoreIndex = fileName.indexOf("__");

        return underscoreIndex >= 0
            ? fileName.substring(0, underscoreIndex).trim() : null;
    };

    const extractFileId = (fileName : string | undefined) => {
        if (fileName === null || fileName === undefined || fileName === "")
        {
            return null;
        }

        let filenameParts = fileName.split("__");

        if (filenameParts.length != 2)
        {
            return null;
        }

        let fileIdWithExtension = filenameParts[1].trim();
        return fileIdWithExtension!.split('.')[0];
    };

    const uploadFileAsync = async (
            file : File,
            idx : number,
            failed : { filename: string; error: string }[])=> {
        let success = false;
        let lastError = '';

        const MAX_RETRIES = 3;
        const RETRY_DELAY = 1000; // 1 second
        const CHUNK_SIZE = 5 * 1024 * 1024; // 5MB
        
        const totalChunks = Math.ceil(file.size / CHUNK_SIZE);
        setUploadProgress(prev => ({...prev, currentChunk: 0, totalChunks: totalChunks > 1 ? totalChunks : 0}));

        if (file.size <= CHUNK_SIZE) {
            // Simple upload for small files
            for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
                try {
                    let data = new FormData()
                    data.append('file', file);

                    const response = await fetch(waleApiBaseUrl + "/BFF/Files/Upload", {
                        method: 'PUT',
                        body: data
                    });

                    if (!response.ok) {
                        throw new Error(`Upload failed with status ${response.status}`);
                    }

                    success = true;
                    break;
                } catch (err) {
                    lastError = err instanceof Error ? err.message : 'Unknown error';
                    if (attempt < MAX_RETRIES) {
                        await new Promise(resolve => setTimeout(resolve, RETRY_DELAY));
                    }
                }
            }
        } else {
            // Chunked upload for large files
            success = true; // Assume success and set to false if any chunk fails
            let currentUploadId: string | null = null;

            for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
                const start = chunkIndex * CHUNK_SIZE;
                const end = Math.min(start + CHUNK_SIZE, file.size);
                const chunk = file.slice(start, end);

                let chunkSuccess = false;
                for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
                    try {
                        let data = new FormData();
                        data.append('file', chunk);
                        data.append('filename', file.name);
                        data.append('chunkIndex', chunkIndex.toString());
                        data.append('totalChunks', totalChunks.toString());

                        if (chunkIndex > 0) {
                            if (currentUploadId) {
                                data.append('uploadId', currentUploadId);
                            } else {
                                throw new Error("Somehow lost track of the upload ID");
                            }
                        }

                        const response = await fetch(waleApiBaseUrl + "/BFF/Files/UploadChunk", {
                            method: 'PUT',
                            body: data
                        });

                        if (!response.ok) {
                            throw new Error(`Chunk ${chunkIndex + 1} upload failed with status ${response.status}`);
                        }

                        if (chunkIndex === 0) {
                            currentUploadId = await response.text();
                        }

                        chunkSuccess = true;
                        setUploadProgress(prev => ({...prev, currentChunk: chunkIndex + 1}));
                        break;
                    } catch (err) {
                        lastError = err instanceof Error ? err.message : 'Unknown error';
                        if (attempt < MAX_RETRIES) {
                            await new Promise(resolve => setTimeout(resolve, RETRY_DELAY));
                        }
                    }
                }

                if (!chunkSuccess) {
                    success = false;
                    break;
                }
            }
        }

        if (!success) {
            failed.push({filename: file.name, error: lastError});
        }

        setUploadProgress(prev => ({...prev, current: idx + 1, currentChunk: 0, totalChunks: 0}));
    };
    
    const dropHandler = useCallback(async (event: React.DragEvent<HTMLDivElement>) => {
        event.stopPropagation();
        event.preventDefault();

        let filesToUpload = event.dataTransfer!.files;
        if (filesToUpload.length === 0) return;

        setUploading(true);
        setSuccessMessage(null);
        setFailedUploads([]);
        setUploadProgress({current: 0, total: filesToUpload.length, currentChunk: 0, totalChunks: 0});
        
        const failed: { filename: string; error: string }[] = [];

        const maxConcurrentScrapers = 5;
        let uploadTasks : any[] = [];
        
        for (let idx = 0; idx < filesToUpload.length; idx++) {
            uploadTasks.push(uploadFileAsync(filesToUpload[idx], idx, failed));

            if (uploadTasks.length != maxConcurrentScrapers) {
                continue;
            }

            while (uploadTasks.length > maxConcurrentScrapers) {
                for (let idx = 0; idx < uploadTasks.length; idx++) {
                    let uploadTask = uploadTasks[idx];
                    await uploadTask;
                }
            }
        }

        if (uploadTasks.length != 0) {
            for (let idx = 0; idx < uploadTasks.length; idx++) {
                let uploadTask = uploadTasks[idx];
                await uploadTask;
            }
        }

        setUploading(false);
        setFailedUploads(failed);

        const successfulCount = filesToUpload.length - failed.length;
        if (successfulCount > 0) {
            setSuccessMessage(`Uploaded ${successfulCount} ${successfulCount === 1 ? 'file' : 'files'} successfully`);
        }

        setLoading(true);
        fetchFiles();
    }, []);

    const downloadErrorCsv = () => {
        if (failedUploads.length === 0) return;

        const headers = ["Filename", "Error"];
        const rows = failedUploads.map(f => [f.filename, `"${f.error.replace(/"/g, '""')}"`]);

        const csvContent = [
            headers.join(","),
            ...rows.map(row => row.join(","))
        ].join("\n");

        const blob = new Blob([csvContent], {type: 'text/csv;charset=utf-8;'});
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.setAttribute("href", url);
        link.setAttribute("download", "upload_errors.csv");
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };

    const downloadCsv = () => {
        if (files.length === 0) return;

        const headers = ["Filename","PermitNumber","FileId"];
        const rows = files.map(file => [file.filename, file.permitNumber, file.fileId]);

        const csvContent = [
            headers.join(","),
            ...rows.map(row => row.join(","))
        ].join("\n");

        const blob = new Blob([csvContent], {type: 'text/csv;charset=utf-8;'});
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.setAttribute("href", url);
        link.setAttribute("download", "files.csv");
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    };
    
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <>
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

            {successMessage && (
                <div style={{
                    backgroundColor: '#d4edda',
                    border: '1px solid #c3e6cb',
                    color: '#155724',
                    padding: '10px',
                    marginBottom: '10px',
                    borderRadius: '4px',
                    position: 'relative'
                }}>
                    <p style={{margin: 0}}>{successMessage}</p>
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
                    <button
                        onClick={downloadErrorCsv}
                        style={{
                            backgroundColor: '#dc3545',
                            color: 'white',
                            border: 'none',
                            padding: '5px 10px',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            marginTop: '10px'
                        }}
                    >
                        Download Error Report CSV
                    </button>
                </div>
            )}

            <div id="dragDropArea"
                 onDrop={(e) => dropHandler(e)}
                 onDragOver={(e) => e.preventDefault()}>
                <p>Drop files here</p>
            </div>

            {!loading && (
                <div style={{marginTop: '20px'}}>
                    <h3>{files.length} {'files'}</h3>
                    {files.length > 0 && (
                        <button
                            onClick={downloadCsv}
                            style={{
                                backgroundColor: '#007bff',
                                color: 'white',
                                border: 'none',
                                padding: '10px 15px',
                                borderRadius: '4px',
                                cursor: 'pointer',
                                marginTop: '10px'
                            }}
                        >
                            Download File List CSV
                        </button>
                    )}
                </div>
            )}

            {loading && files.length > 0 && (
                <div className="container"><p>Loading ({files.length} files)...</p></div>
            )}
            {loading && files.length === 0 && (
                <div className="container"><p>Loading...</p></div>
            )}
        </>
    );
}

export default FilesList;