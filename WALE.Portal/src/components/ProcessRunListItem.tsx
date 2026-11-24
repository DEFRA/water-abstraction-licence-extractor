import {useState} from 'react';
import {Link} from 'react-router-dom';
import {ProcessRun, ProcessRunNameRequest} from '../api/generated/apiClient';
import {processRunFigures} from '../utils/processRunUtils.ts';
import waleApiClient from '../api/apiClient.ts';

interface ProcessRunListItemProps {
    run: ProcessRun;
}

function ProcessRunListItem({run}: ProcessRunListItemProps) {
    const linkTo = run.documentType === 'WrInspectionReport'
        ? {pathname: '/inspectionReport', search: `?processRunId=${run.processRunId}`}
        : {pathname: '/listSearch', search: `?processRunId=${run.processRunId}`};

    const [name, setName] = useState(run.name ?? '');
    const [draft, setDraft] = useState(name);
    const [isEditing, setIsEditing] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [isHovered, setIsHovered] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const save = async () => {
        if (draft === name) {
            setIsEditing(false);
            return;
        }

        setIsSaving(true);
        setError(null);

        try {
            await waleApiClient.updateProcessRunName(run.processRunId!, new ProcessRunNameRequest({name: draft}));
            setName(draft);
            setIsEditing(false);
        } catch {
            setError('Could not save');
        } finally {
            setIsSaving(false);
        }
    };

    const cancel = () => {
        setDraft(name);
        setIsEditing(false);
        setError(null);
    };

    return (
        <li className="process-run-list-item"
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}>
            <Link to={linkTo}>
                {run.processRunId} - {run.startDateTimeUtc?.toLocaleString()}
            </Link>
            &nbsp;-&nbsp;
            {isEditing ? (
                <>
                    <input
                        autoFocus
                        value={draft}
                        disabled={isSaving}
                        size={60}
                        placeholder="Clear to show the generated description"
                        onChange={event => setDraft(event.target.value)}
                        onBlur={save}
                        onKeyDown={event => {
                            if (event.key === 'Enter') {
                                void save();
                            } else if (event.key === 'Escape') {
                                cancel();
                            }
                        }}
                    />
                    {error && <span style={{color: 'crimson', marginLeft: '6px'}}>{error}</span>}
                </>
            ) : (
                <>
                    <span title={run.description ?? undefined}>
                        {name || run.description}
                    </span>
                    <button
                        type="button"
                        onClick={() => {
                            setDraft(name);
                            setIsEditing(true);
                        }}
                        style={{
                            marginLeft: '6px',
                            fontSize: '0.75rem',
                            cursor: 'pointer',
                            visibility: isHovered ? 'visible' : 'hidden'
                        }}>
                        Edit
                    </button>
                </>
            )}
            &nbsp;({processRunFigures(run)})
            {((run.filesErrored ?? 0) + (run.filesTransientErrored ?? 0)) > 0 && (
                <>
                    &nbsp;
                    <Link to={{pathname: '/errors', search: `?processRunId=${run.processRunId}`}}>
                        failed files
                    </Link>
                </>
            )}
        </li>
    );
}

export default ProcessRunListItem;
