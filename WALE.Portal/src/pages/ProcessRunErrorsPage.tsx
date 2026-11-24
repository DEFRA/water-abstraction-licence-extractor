import {useEffect, useMemo, useState} from 'react';
import {Link, useSearchParams} from 'react-router-dom';
import {MatchResultError, ProcessRunFailureGroup} from '../api/generated/apiClient';
import {waleApiClient} from '../api/apiClient';

const STATUS_FILTERS = [
    {value: '', label: 'All non-Ok'},
    {value: 'Error', label: 'Error'},
    {value: 'TransientError', label: 'Transient error'},
    {value: 'InProgress', label: 'In progress'}
];

const PAGE_SIZES = [50, 100, 500];

function firstLine(error?: string) {
    return (error ?? '').split('\n')[0].trim();
}

function ProcessRunErrorsPage() {
    const [searchParams] = useSearchParams();
    const processRunId = Number(searchParams.get('processRunId'));

    const [status, setStatus] = useState(searchParams.get('status') ?? '');
    const [exception, setException] = useState(searchParams.get('exception') ?? '');
    const [pageSize, setPageSize] = useState(PAGE_SIZES[0]);
    const [pageNumber, setPageNumber] = useState(1);

    const [items, setItems] = useState<MatchResultError[]>([]);
    const [totalCount, setTotalCount] = useState(0);
    const [declared, setDeclared] = useState(0);
    const [processed, setProcessed] = useState(0);
    const [groups, setGroups] = useState<ProcessRunFailureGroup[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [expanded, setExpanded] = useState<number[]>([]);

    useEffect(() => {
        let cancelled = false;

        const fetchPage = async () => {
            setLoading(true);
            setError(null);

            try {
                const response = await waleApiClient.getProcessRunNonOkFiles(
                    processRunId,
                    status === '' ? undefined : status,
                    exception === '' ? undefined : exception,
                    (pageNumber - 1) * pageSize,
                    pageSize);

                if (cancelled) {
                    return;
                }

                setItems(response.items ?? []);
                setTotalCount(response.totalCount ?? 0);
                setDeclared(response.declaredFileCount ?? 0);
                setProcessed(response.processedFileCount ?? 0);
                setGroups(response.failureGroups ?? []);
            } catch (err) {
                if (!cancelled) {
                    setError(err instanceof Error ? err.message : 'Failed to fetch failed files');
                }
            } finally {
                if (!cancelled) {
                    setLoading(false);
                }
            }
        };

        if (Number.isFinite(processRunId) && processRunId > 0) {
            void fetchPage();
        } else {
            setLoading(false);
            setError('No processRunId given');
        }

        return () => {
            cancelled = true;
        };
    }, [processRunId, status, exception, pageNumber, pageSize]);

    const totalPages = useMemo(
        () => (totalCount > 0 ? Math.ceil(totalCount / pageSize) : 0),
        [totalCount, pageSize]);

    const toggle = (matchesResultId: number) =>
        setExpanded(current => current.includes(matchesResultId)
            ? current.filter(id => id !== matchesResultId)
            : [...current, matchesResultId]);

    return (
        <div className="list-page-container">
            <h1>
                <Link to="/">Process runs</Link>
                {` / run ${searchParams.get('processRunId') ?? '?'} / failed files`}
            </h1>

            <div style={{marginBottom: '10px'}}>
                <label>
                    Status:{' '}
                    <select
                        value={status}
                        onChange={event => {
                            setStatus(event.target.value);
                            setPageNumber(1);
                        }}>
                        {STATUS_FILTERS.map(filter => (
                            <option key={filter.value} value={filter.value}>{filter.label}</option>
                        ))}
                    </select>
                </label>
                {'   '}
                <label>
                    Page size:{' '}
                    <select
                        value={pageSize}
                        onChange={event => {
                            setPageSize(Number(event.target.value));
                            setPageNumber(1);
                        }}>
                        {PAGE_SIZES.map(size => (
                            <option key={size} value={size}>{size}</option>
                        ))}
                    </select>
                </label>
                {'   '}
                <strong>{totalCount.toLocaleString()}</strong> file(s)
                {totalPages > 1 && ` · page ${pageNumber} of ${totalPages}`}
            </div>

            {/* Run 267 processed 1,926 files against 1,831 declared, cause unknown, so a
                negative shortfall is withheld rather than shown as a figure. */}
            {declared > processed && (
                <p>
                    <strong>{(declared - processed).toLocaleString()}</strong> of the{' '}
                    {declared.toLocaleString()} files in this run's work list produced no result
                    row of any kind, so they are not listed below - the run never took a lock on
                    them.
                </p>
            )}

            {groups.length > 0 && (
                <table style={{marginBottom: '12px'}}>
                    <thead>
                    <tr>
                        <th>Failures by type</th>
                        <th>Files</th>
                    </tr>
                    </thead>
                    <tbody>
                    {groups.map(group => (
                        <tr key={group.exception ?? '(none)'}>
                            <td>
                                <a
                                    href="#"
                                    style={{fontWeight: exception === group.exception ? 'bold' : 'normal'}}
                                    onClick={event => {
                                        event.preventDefault();
                                        setException(exception === group.exception ? '' : (group.exception ?? ''));
                                        setPageNumber(1);
                                    }}>
                                    {(group.exception ?? '(not recorded)').split('.').pop()}
                                </a>
                            </td>
                            <td>{group.files?.toLocaleString()}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            )}

            {exception !== '' && (
                <p>
                    Showing {exception.split('.').pop()} only.{' '}
                    <a href="#" onClick={event => {
                        event.preventDefault();
                        setException('');
                        setPageNumber(1);
                    }}>Show all</a>
                </p>
            )}

            {loading && <p>Loading...</p>}
            {error && <p className="error">Error: {error}</p>}

            {!loading && !error && items.length === 0 && <p>No failed files for this run.</p>}

            {!loading && !error && items.length > 0 && (
                <table>
                    <thead>
                    <tr>
                        <th>Filename</th>
                        <th>Status</th>
                        <th>Reason</th>
                        <th>When</th>
                    </tr>
                    </thead>
                    <tbody>
                    {items.map(item => (
                        <tr key={item.matchesResultId}>
                            <td className="filename-cell">{item.filename}</td>
                            <td>{item.status ?? '(none)'}</td>
                            <td>
                                {firstLine(item.error) === ''
                                    ? '(not recorded)'
                                    : (
                                        <>
                                            <a
                                                href="#"
                                                onClick={event => {
                                                    event.preventDefault();
                                                    toggle(item.matchesResultId!);
                                                }}>
                                                {firstLine(item.error)}
                                            </a>
                                            {expanded.includes(item.matchesResultId!) && (
                                                <pre style={{whiteSpace: 'pre-wrap', fontSize: '0.75rem'}}>
                                                    {item.error}
                                                </pre>
                                            )}
                                        </>
                                    )}
                            </td>
                            <td>{item.dateTimeUtc?.toLocaleString()}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            )}

            {totalPages > 1 && (
                <div style={{marginTop: '10px'}}>
                    <button
                        type="button"
                        disabled={pageNumber <= 1}
                        onClick={() => setPageNumber(pageNumber - 1)}>
                        Prev
                    </button>
                    {` ${pageNumber} / ${totalPages} `}
                    <button
                        type="button"
                        disabled={pageNumber >= totalPages}
                        onClick={() => setPageNumber(pageNumber + 1)}>
                        Next
                    </button>
                </div>
            )}
        </div>
    );
}

export default ProcessRunErrorsPage;
