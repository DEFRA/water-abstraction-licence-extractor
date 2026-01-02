import {Link} from 'react-router-dom';
import {ProcessRun} from '../api/generated/apiClient';

interface ProcessRunListItemProps {
    run: ProcessRun;
}

function ProcessRunListItem({run}: ProcessRunListItemProps) {
    return (
        <li className="process-run-list-item">
            <Link to={{
                pathname: '/list',
                search: `?processRunId=${run.processRunId}`,
            }}>
                {run.processRunId} - {run.startDateTimeUtc?.toLocaleString()}
            </Link>
            - {run.description} ({run.numberOfFiles} files)
        </li>
    );
}

export default ProcessRunListItem;