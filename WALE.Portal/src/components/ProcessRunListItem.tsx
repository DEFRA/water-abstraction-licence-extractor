import {Link} from 'react-router-dom';
import {ProcessRun} from '../api/generated/apiClient';
import {processRunFigures} from '../utils/processRunUtils.ts';

interface ProcessRunListItemProps {
    run: ProcessRun;
}

function ProcessRunListItem({run}: ProcessRunListItemProps) {
    const linkTo = run.documentType === 'WrInspectionReport'
        ? {pathname: '/inspectionReport', search: `?processRunId=${run.processRunId}`}
        : {pathname: '/listSearch', search: `?processRunId=${run.processRunId}`};

    return (
        <li className="process-run-list-item">
            <Link to={linkTo}>
                {run.processRunId} - {run.startDateTimeUtc?.toLocaleString()}
            </Link>
            &nbsp;- {run.description} ({processRunFigures(run)})
        </li>
    );
}

export default ProcessRunListItem;
