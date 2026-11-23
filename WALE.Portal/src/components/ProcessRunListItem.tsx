import {Link} from 'react-router-dom';
import {ProcessRun} from '../api/generated/apiClient';

interface ProcessRunListItemProps {
    run: ProcessRun;
}

function figures(run: ProcessRun) {
    const count = (n?: number) => (n ?? 0).toLocaleString();
    const parts = [`${count(run.filesProcessed)} files`];

    if ((run.filesErrored ?? 0) > 0) {
        parts.push(`${count(run.filesErrored)} errors`);
    }

    if ((run.licenceCount ?? 0) > 0) {
        parts.push(run.liveLicenceCount === run.licenceCount
            ? `${count(run.licenceCount)} licences`
            : `${count(run.licenceCount)} licences, ${count(run.liveLicenceCount)} live`);
    }

    if (run.status && run.status !== 'Completed') {
        parts.push(run.status);
    }

    return parts.join(' · ');
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
            &nbsp;- {run.description} ({figures(run)})
        </li>
    );
}

export default ProcessRunListItem;
