import FilesListItem from '../components/FilesListItem.tsx';
import {type ChangeEvent, useEffect, useState} from 'react'
import {FilePdf} from "../class/FilePdf.tsx";

interface FilesListProps {
    onFilesSelected: (event: ChangeEvent<HTMLInputElement>) => void;
}

export function FilesList({onFilesSelected}: FilesListProps) {
    const [files, setFiles] = useState<FilePdf[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
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
            let response = await fetch("http://localhost:8080/BFF/Files/ListAll");
            let items = await response.json();
            
            items = items.map(function(item: string | undefined) {
                let returnItem = new FilePdf();
                returnItem.filename = item;
                
                return returnItem;
            });
            
            return items;
        };
        
        fetchFiles();
    }, []);

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

            <input type="file" id="filesUpload" multiple onChange={onFilesSelected} />
        </>
    );
}

export default FilesList;