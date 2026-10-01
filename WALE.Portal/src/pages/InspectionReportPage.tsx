import {useSearchParams} from 'react-router-dom';
import {Fragment, useState, useEffect, useMemo, useRef} from 'react';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import {waleApiClient, waleApiBaseUrl} from '../api/apiClient';
import {ScrapeDocuments} from '../components/ScrapeDocuments';
import {ExportReport} from '../components/ExportReport';
import {InspectionReportModal} from '../components/InspectionReportModal';

interface SimpleMatchResult {
    fileId: string;
    filename: string | null;
    status: string;
}

// Matching the licence list's own page-size options
const PAGE_SIZES = [10, 100, 500, 1000];

interface FileDetails {
    template?: string;
    date?: string;
    completeness?: number;
    isScan?: boolean;
}

// Unknown means classification failed outright; NonStandardNarrative means the document didn't
// match the client's expected format and fell back to generic rules
const LOW_CONFIDENCE_TEMPLATES = new Set(['unknown', 'nonStandardNarrative']);

function InspectionReportPage() {
    const [searchParams] = useSearchParams();
    const processRunId = Number(searchParams.get('processRunId'));

    const [files, setFiles] = useState<SimpleMatchResult[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [filterText, setFilterText] = useState('');
    const [statusFilter, setStatusFilter] = useState('');
    const [templateFilter, setTemplateFilter] = useState('');
    const [scanFilter, setScanFilter] = useState<'' | 'scan' | 'native'>('');

    const [inlineFileId, setInlineFileId] = useState<string | null>(null);
    const [inlineJson, setInlineJson] = useState<unknown>(null);
    const [inlineLoading, setInlineLoading] = useState(false);

    const [detailsByFileId, setDetailsByFileId] = useState<Record<string, FileDetails>>({});

    const [modalFileId, setModalFileId] = useState<string | null>(null);

    const [activeTab, setActiveTab] = useState<'files' | 'actions'>('files');

    type SortField = 'filename' | 'status' | 'date' | 'template' | 'completeness' | 'isScan';
    const [sortField, setSortField] = useState<SortField | ''>('');
    const [sortAscending, setSortAscending] = useState(true);

    const [pageNumber, setPageNumber] = useState(1);
    const [pageSize, setPageSize] = useState(100);

    // Fetched per visible page rather than with the file list: a presigned URL is ~1.5kb, so
    // returning one per row made the list response 29mb against 2.4mb for the rows alone.
    const [thumbnailUrls, setThumbnailUrls] = useState<Record<string, string>>({});

    const handleSort = (field: SortField) => {
        setSortAscending(previous => (sortField === field ? !previous : true));
        setSortField(field);
    };

    useEffect(() => {
        if (!processRunId) return;

        setLoading(true);
        fetch(`${waleApiBaseUrl}/BFF/FileData/GetSimpleMatchResults?processRunId=${processRunId}`)
            .then(response => {
                if (!response.ok) throw new Error(`Failed to load files: ${response.status}`);
                return response.json();
            })
            .then((data: SimpleMatchResult[]) => setFiles(data))
            .catch(err => setError(err instanceof Error ? err.message : 'Failed to fetch files'))
            .finally(() => setLoading(false));
    }, [processRunId]);

    // One request for the whole run. This used to call WrInspectionReportString once per file,
    // which is 17,000+ requests on a full process run - the columns these populate are also what
    // the template/scan filters and date/completeness sorts work from, so they have to cover
    // every row, not just the visible page.
    useEffect(() => {
        if (!processRunId) return;

        let cancelled = false;

        fetch(`${waleApiBaseUrl}/BFF/FileData/GetWrInspectionReportSummaries?processRunId=${processRunId}`)
            .then(response => response.ok ? response.json() : [])
            .then((summaries: (FileDetails & {fileId: string})[]) => {
                if (cancelled) return;

                setDetailsByFileId(Object.fromEntries(
                    summaries.map(({fileId, ...details}) => [fileId, details])));
            })
            .catch(err => console.error('Error fetching inspection report summaries:', err));

        return () => {
            cancelled = true;
        };
    }, [processRunId]);

    const statuses = useMemo(
        () => Array.from(new Set(files.map(f => f.status))).sort(),
        [files]
    );

    const templates = useMemo(
        () => Array.from(new Set(
            Object.values(detailsByFileId)
                .map(d => d.template)
                .filter((t): t is string => !!t)
        )).sort(),
        [detailsByFileId]
    );

    const filteredFiles = useMemo(() => {
        const getSortValue = (file: SimpleMatchResult, field: SortField): string | number | undefined => {
            switch (field) {
                case 'filename': return file.filename ?? undefined;
                case 'status': return file.status;
                case 'date': return detailsByFileId[file.fileId]?.date;
                case 'template': return detailsByFileId[file.fileId]?.template;
                case 'completeness': return detailsByFileId[file.fileId]?.completeness;
                case 'isScan': {
                    const isScan = detailsByFileId[file.fileId]?.isScan;
                    return isScan === undefined ? undefined : (isScan ? 1 : 0);
                }
            }
        };

        const term = filterText.trim().toLowerCase();
        const matching = files.filter(f => {
            const isScan = detailsByFileId[f.fileId]?.isScan;

            return (term === '' || (f.filename ?? '').toLowerCase().includes(term))
                && (statusFilter === '' || f.status === statusFilter)
                && (templateFilter === '' || detailsByFileId[f.fileId]?.template === templateFilter)
                && (scanFilter === '' || (scanFilter === 'scan' ? isScan === true : isScan === false));
        });

        if (!sortField) return matching;

        return [...matching].sort((a, b) => {
            const valueA = getSortValue(a, sortField);
            const valueB = getSortValue(b, sortField);

            if (valueA === undefined && valueB === undefined) return 0;
            if (valueA === undefined) return 1;
            if (valueB === undefined) return -1;

            const comparison = valueA < valueB ? -1 : valueA > valueB ? 1 : 0;
            return sortAscending ? comparison : -comparison;
        });
    }, [files, filterText, statusFilter, templateFilter, scanFilter, detailsByFileId, sortField, sortAscending]);

    const totalPages = Math.max(1, Math.ceil(filteredFiles.length / pageSize));

    const pagedFiles = useMemo(
        () => filteredFiles.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
        [filteredFiles, pageNumber, pageSize]);

    // Filtering can shrink the list below the current page.
    useEffect(() => {
        if (pageNumber > totalPages) setPageNumber(1);
    }, [pageNumber, totalPages]);

    // First and last page, plus a couple either side of the current one; null is a gap.
    const pageWindow = useMemo(() => {
        const pages = new Set<number>([1, totalPages]);

        for (let page = pageNumber - 2; page <= pageNumber + 2; page++) {
            if (page >= 1 && page <= totalPages) pages.add(page);
        }

        const ordered = [...pages].sort((a, b) => a - b);

        return ordered.flatMap((page, index) =>
            index > 0 && page - ordered[index - 1] > 1 ? [null, page] : [page]);
    }, [pageNumber, totalPages]);

    // Keyed on the ids themselves, not the array: detailsByFileId streams in per file, so
    // pagedFiles gets a fresh identity thousands of times and an effect depending on it would
    // re-fire (and cancel its own in-flight fetch) on every one of them. The ref tracks what has
    // already been asked for, so a page is only ever fetched once.
    const requestedThumbnails = useRef<Set<string>>(new Set());
    const pagedFileIds = pagedFiles.map(file => file.fileId).join(',');

    useEffect(() => {
        const missing = (pagedFileIds === '' ? [] : pagedFileIds.split(','))
            .filter(fileId => !requestedThumbnails.current.has(fileId));

        if (missing.length === 0) return;

        missing.forEach(fileId => requestedThumbnails.current.add(fileId));

        fetch(`${waleApiBaseUrl}/Extractor/Images/GeneratePresignedUrls`, {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({fileIds: missing, templateUrl: 'thumbnail_{0}.jpg'})
        })
            .then(response => response.ok ? response.json() : {})
            .then((urls: Record<string, string>) => setThumbnailUrls(previous => ({...previous, ...urls})))
            .catch(() => {
                // Decorative - let a failure retry on the next visit to this page.
                missing.forEach(fileId => requestedThumbnails.current.delete(fileId));
            });
    }, [pagedFileIds]);

    const toggleInline = (fileId: string) => {
        if (inlineFileId === fileId) {
            setInlineFileId(null);
            setInlineJson(null);
            return;
        }

        setInlineFileId(fileId);
        setInlineLoading(true);
        waleApiClient.matchesResultString(fileId)
            .then(text => setInlineJson(text ? JSON.parse(text) : null))
            .catch(err => console.error('Error fetching matches result:', err))
            .finally(() => setInlineLoading(false));
    };

    // @ts-ignore
    const toHome = () => window.location = '/';

    if (loading) return <div className="container"><p>Loading...</p></div>;
    if (error) return <div className="container error"><p>Error: {error}</p></div>;

    return (
        <div className="list-page-container">
            <div style={{position: 'absolute', top: 5, left: 5, cursor: 'pointer'}} onClick={toHome}>&#8617;</div>

            <h1>
                Inspection Report Files - Process Run {processRunId}
                {' | '}
                <a
                    href="#"
                    className={activeTab === 'files' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('files');
                    }}>
                    Files
                </a>
                {' | '}
                <a
                    href="#"
                    className={activeTab === 'actions' ? 'selected' : ''}
                    onClick={(e) => {
                        e.preventDefault();
                        setActiveTab('actions');
                    }}>
                    Actions
                </a>
            </h1>

            {activeTab === 'actions' && (
                <div id="actions">
                    <ScrapeDocuments documentType="WrInspectionReport"/>
                    <ExportReport processRunId={processRunId}/>
                </div>
            )}

            {activeTab === 'files' && (
            <>
            <div style={{clear: 'both', display: 'block', width: '100%', marginTop: '10px', marginBottom: '10px'}}>
                {filteredFiles.length} of {files.length} file(s) : Page {pageNumber} of {totalPages}&nbsp;&nbsp;&nbsp;

                <label>
                    Page size:{' '}
                    <select
                        value={pageSize}
                        onChange={(e) => {
                            setPageSize(Number(e.target.value));
                            setPageNumber(1);
                        }}
                        style={{width: '60px'}}>
                        {PAGE_SIZES.map(size => <option key={size} value={size}>{size}</option>)}
                    </select>
                </label>

                &nbsp;&nbsp;&nbsp;

                {pageNumber > 1 && (
                    <>
                        <a href="#" onClick={(e) => { e.preventDefault(); setPageNumber(pageNumber - 1); }}>Prev</a>
                        {' | '}
                    </>
                )}

                {/* Windowed, unlike the licence list's full page list - a process run is tens of
                    thousands of files, so every page number would be thousands of links. */}
                {pageWindow.map((page, index) => (
                    <span key={`${page}-${index}`}>
                        {page === null
                            ? <>&hellip; </>
                            : page === pageNumber
                                ? <strong>{page}</strong>
                                : <a href="#" onClick={(e) => { e.preventDefault(); setPageNumber(page); }}>{page}</a>}
                        {' '}
                    </span>
                ))}

                {totalPages > pageNumber && (
                    <>
                        {' | '}
                        <a href="#" onClick={(e) => { e.preventDefault(); setPageNumber(pageNumber + 1); }}>Next</a>
                    </>
                )}
            </div>

            <table>
                <thead>
                <tr>
                    <td></td>
                    <td>
                        <input
                            type="text"
                            placeholder="Filter by filename..."
                            value={filterText}
                            onChange={(e) => setFilterText(e.target.value)}
                        />
                    </td>
                    <td>
                        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
                            <option value="">All statuses</option>
                            {statuses.map(status => (
                                <option key={status} value={status}>{status}</option>
                            ))}
                        </select>
                    </td>
                    <td></td>
                    <td>
                        <select value={templateFilter} onChange={(e) => setTemplateFilter(e.target.value)}>
                            <option value="">All templates</option>
                            {templates.map(template => (
                                <option key={template} value={template}>{template}</option>
                            ))}
                        </select>
                    </td>
                    <td>
                        <select value={scanFilter} onChange={(e) => setScanFilter(e.target.value as '' | 'scan' | 'native')}>
                            <option value="">All</option>
                            <option value="scan">Scanned</option>
                            <option value="native">Native</option>
                        </select>
                    </td>
                    <td></td>
                    <td></td>
                </tr>
                <tr>
                    <th style={{textAlign: 'left'}}>Thumbnail</th>
                    <th style={{textAlign: 'left'}}>
                        Filename <a href="#" onClick={(e) => { e.preventDefault(); handleSort('filename'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>
                        Status <a href="#" onClick={(e) => { e.preventDefault(); handleSort('status'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>
                        Date <a href="#" onClick={(e) => { e.preventDefault(); handleSort('date'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>
                        Template <a href="#" onClick={(e) => { e.preventDefault(); handleSort('template'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>
                        Scan? <a href="#" onClick={(e) => { e.preventDefault(); handleSort('isScan'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>
                        Completeness <a href="#" onClick={(e) => { e.preventDefault(); handleSort('completeness'); }}>&#8693;</a>
                    </th>
                    <th style={{textAlign: 'left'}}>View</th>
                </tr>
                </thead>
                <tbody>
                {pagedFiles.map(file => {
                    const details = detailsByFileId[file.fileId];
                    const lowConfidence = !!details?.template && LOW_CONFIDENCE_TEMPLATES.has(details.template);

                    return (
                    <Fragment key={file.fileId}>
                        <tr style={lowConfidence ? {backgroundColor: '#fff8e1'} : undefined}>
                            <td>
                                {thumbnailUrls[file.fileId] && (
                                    <img
                                        src={thumbnailUrls[file.fileId]}
                                        width={80}
                                        alt=""
                                        loading="lazy"
                                        onError={(e) => {
                                            e.currentTarget.style.display = 'none';
                                        }}
                                    />
                                )}
                            </td>
                            <td>
                                <a href="#" onClick={(e) => {
                                    e.preventDefault();
                                    toggleInline(file.fileId);
                                }}>
                                    {file.filename}
                                </a>
                            </td>
                            <td>{file.status}</td>
                            <td>{details ? (details.date ?? '-') : '...'}</td>
                            <td>
                                {details ? (details.template ?? '-') : '...'}
                                {lowConfidence && (
                                    <span
                                        title="Classified as Unknown or NonStandardNarrative - little to no template-specific rule tuning applies, worth a manual check"
                                        style={{marginLeft: '6px', cursor: 'help'}}
                                    >
                                        &#9888;
                                    </span>
                                )}
                            </td>
                            <td>{details ? (details.isScan === undefined ? '-' : (details.isScan ? 'Yes' : 'No')) : '...'}</td>
                            <td>{details ? (details.completeness !== undefined ? `${details.completeness}%` : '-') : '...'}</td>
                            <td>
                                <a href="#" onClick={(e) => {
                                    e.preventDefault();
                                    setModalFileId(file.fileId);
                                }}>
                                    View PDF
                                </a>
                            </td>
                        </tr>
                        {inlineFileId === file.fileId && (
                            <tr>
                                <td colSpan={8} style={{padding: '10px', backgroundColor: '#FAFAFA'}}>
                                    {inlineLoading
                                        ? <p>Loading...</p>
                                        : <JsonView src={inlineJson} collapsed={1} theme="default"/>}
                                </td>
                            </tr>
                        )}
                    </Fragment>
                    );
                })}
                </tbody>
            </table>
            </>
            )}

            {modalFileId && (
                <InspectionReportModal fileId={modalFileId} processRunId={processRunId} onClose={() => setModalFileId(null)}/>
            )}
        </div>
    );
}

export default InspectionReportPage;
