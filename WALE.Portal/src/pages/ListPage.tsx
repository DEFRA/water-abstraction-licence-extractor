import { useSearchParams } from 'react-router-dom';

function ListPage() {
    const [searchParams] = useSearchParams();
    const processRunId = searchParams.get('processRunId');
    
    return(
        <div>
            <p>Selected process run ID = {processRunId}</p>
        </div>);
}

export default ListPage;