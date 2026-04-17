import FilesListItem from '../components/FilesListItem.tsx';
import {useEffect, useState} from 'react'
import {FilePdf} from "../class/FilePdf.tsx";

interface FilesListProps {}

export function FilesList({}: FilesListProps) {
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
            let file = new FilePdf();
            file.filename = 'xx1';
            
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
                            <FilesListItem file={file} />
                        ))}
                    </ul>
                )}
        </>
    );
}

export default FilesList;