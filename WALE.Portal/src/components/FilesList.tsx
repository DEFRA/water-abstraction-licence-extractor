import FilesListItem from '../components/FilesListItem.tsx';
import {type ChangeEvent, useCallback, useEffect, useState} from 'react'
import {FilePdf} from "../class/FilePdf.tsx";
import {waleApiBaseUrl} from "../api/apiClient.ts";

interface FilesListProps {}

export function FilesList({}: FilesListProps) {
    const [files, setFiles] = useState<FilePdf[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

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
    
    const fileUploaded = useCallback(async (file: ChangeEvent<HTMLInputElement>) => {
        for (let idx = 0, len = file.target.files!.length; idx < len; idx++) {
            let data = new FormData()
            data.append('file', file.target.files![idx]);

            setLoading(true);
        
            await fetch(waleApiBaseUrl + "/BFF/Files/Upload", {
                method: 'PUT',
                body: data
                });

            await fetchFiles();
        }
    }, []);

    if (loading && files.length > 0) return <div className="container"><p>Loading ({files.length} files)...</p></div>;
    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;
    
    return (
        <>
            {files.length === 0
                ? (<p>No files found.</p>)
                : (
                    <ul id="filesList">
                        {files.map((file) => (
                            <FilesListItem file={file} key={file.filename} />
                        ))}
                    </ul>
                )}

            <input type="file" id="filesUpload" multiple onChange={fileUploaded} />
        </>
    );
}

export default FilesList;