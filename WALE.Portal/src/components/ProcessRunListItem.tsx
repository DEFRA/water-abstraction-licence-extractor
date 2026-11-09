import {Link} from 'react-router-dom';
import {ProcessRun} from '../api/generated/apiClient';

interface ProcessRunListItemProps {
    run: ProcessRun;
}

function ProcessRunListItem({run}: ProcessRunListItemProps) {
    const documentType = (run as unknown as { documentType?: string }).documentType;
    const linkTo = documentType === 'WrInspectionReport'
        ? {pathname: '/inspectionReport', search: `?processRunId=${run.processRunId}`}
        : {pathname: '/listSearch', search: `?processRunId=${run.processRunId}`};

    return (
        <li className="process-run-list-item">
            <Link to={linkTo}>
                {run.processRunId} - {run.startDateTimeUtc?.toLocaleString()}
            </Link>
             &nbsp;- {run.description} ({run.successCount} files found |  {run.numberOfFilesNotFound} files not found | Total {run.numberOfFiles} files)
        </li>
    );
}

export default ProcessRunListItem;