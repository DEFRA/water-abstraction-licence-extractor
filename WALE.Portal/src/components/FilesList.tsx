import FilesListItem from '../components/FilesListItem.tsx';
import {useCallback, useEffect, useState} from 'react'
import {FilePdf} from "../class/FilePdf.tsx";
import {waleApiBaseUrl} from "../api/apiClient.ts";

interface FilesListProps {}

export function FilesList({}: FilesListProps) {
    const [files, setFiles] = useState<FilePdf[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [uploading, setUploading] = useState(false);
    const [uploadProgress, setUploadProgress] = useState({ current: 0, total: 0 });
    const [successMessage, setSuccessMessage] = useState<string | null>(null);

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

        items = items.map(function(item: string | undefined) {
            let returnItem = new FilePdf();
            returnItem.filename = item;

            return returnItem;
        });

        return items;
    };
    
    const dropHandler = useCallback(async (event: React.DragEvent<HTMLDivElement>) => {
        event.stopPropagation();
        event.preventDefault();
        
        let filesToUpload = event.dataTransfer!.files;
        if (filesToUpload.length === 0) return;

        setUploading(true);
        setSuccessMessage(null);
        setUploadProgress({ current: 0, total: filesToUpload.length });

        for (let idx = 0; idx < filesToUpload.length; idx++) {
            let data = new FormData()
            data.append('file', filesToUpload[idx]);

            await fetch(waleApiBaseUrl + "/BFF/Files/Upload", {
                method: 'PUT',
                body: data
            });

            setUploadProgress(prev => ({ ...prev, current: idx + 1 }));
            await fetchFiles();
        }

        setUploading(false);
        setSuccessMessage(`Uploaded ${filesToUpload.length} ${filesToUpload.length === 1 ? 'file' : 'files'} successfully`);
        
        // Clear success message after 5 seconds
        setTimeout(() => setSuccessMessage(null), 5000);
    }, []);

    if (loading && files.length > 0) return <div className="container"><p>Loading ({files.length} files)...</p></div>;
    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;
    
    return (
        <>
            {uploading && (
                <div style={{ backgroundColor: '#e7f3ff', border: '1px solid #b3d7ff', padding: '10px', marginBottom: '10px', borderRadius: '4px' }}>
                    <p style={{ margin: 0 }}>Uploading {uploadProgress.current} of {uploadProgress.total} files...</p>
                </div>
            )}
            
            {successMessage && (
                <div style={{ backgroundColor: '#d4edda', border: '1px solid #c3e6cb', color: '#155724', padding: '10px', marginBottom: '10px', borderRadius: '4px' }}>
                    <p style={{ margin: 0 }}>{successMessage}</p>
                </div>
            )}

            <div id="dragDropArea"
                onDrop={(e) => dropHandler(e)}
                onDragOver={(e) => e.preventDefault()}></div>

            {files.length === 0
                ? (<p>No files found.</p>)
                : (
                    <ul id="filesList">
                        {files.map((file) => (
                            <FilesListItem file={file} key={file.filename}/>
                        ))}
                    </ul>
                )}
        </>
    );
}

export default FilesList;