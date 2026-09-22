import {useSearchParams} from 'react-router-dom';
import {Fragment, useState, useEffect, useMemo} from 'react';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import {waleApiClient, waleApiBaseUrl} from '../api/apiClient';
import {ScrapeDocuments} from '../components/ScrapeDocuments';
import {ExportReport} from '../components/ExportReport';

interface SimpleMatchResult {
    fileId: string;
    filename: string;
    status: string;
}

interface FileDetails {
    template?: string;
    date?: string;
}

function InspectionReportPage() {
    const [searchParams] = useSearchParams();
    const processRunId = Number(searchParams.get('processRunId'));

    const [files, setFiles] = useState<SimpleMatchResult[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const [filterText, setFilterText] = useState('');
    const [statusFilter, setStatusFilter] = useState('');
    const [templateFilter, setTemplateFilter] = useState('');

    const [inlineFileId, setInlineFileId] = useState<string | null>(null);
    const [inlineJson, setInlineJson] = useState<unknown>(null);
    const [inlineLoading, setInlineLoading] = useState(false);

    const [detailsByFileId, setDetailsByFileId] = useState<Record<string, FileDetails>>({});

    const [activeTab, setActiveTab] = useState<'files' | 'actions'>('files');

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

    useEffect(() => {
        if (files.length === 0) return;

        let cancelled = false;
        const concurrency = 10;
        const queue = [...files];

        const worker = async () => {
            while (!cancelled) {
                const file = queue.shift();
                if (!file) return;

                try {
                    const response = await fetch(`${waleApiBaseUrl}/BFF/FileData/WrInspectionReportString?fileId=${file.fileId}&processRunId=${processRunId}`);
                    const text = await response.text();
                    const wrInspectionReport = text ? JSON.parse(text) : null;

                    setDetailsByFileId(previous => ({
                        ...previous,
                        [file.fileId]: {
                            template: wrInspectionReport?.metadata?.template,
                            date: wrInspectionReport?.inspectionDate?.dateTime?.split('T')[0]
                                ?? wrInspectionReport?.metadata?.date?.date
                        }
                    }));
                } catch (err) {
                    console.error(`Error fetching WrInspectionReport for ${file.fileId}:`, err);
                }
            }
        };

        Array.from({length: concurrency}, worker);

        return () => {
            cancelled = true;
        };
    }, [files, processRunId]);

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
        const term = filterText.trim().toLowerCase();
        return files.filter(f =>
            (term === '' || f.filename.toLowerCase().includes(term)) &&
            (statusFilter === '' || f.status === statusFilter) &&
            (templateFilter === '' || detailsByFileId[f.fileId]?.template === templateFilter)
        );
    }, [files, filterText, statusFilter, templateFilter, detailsByFileId]);

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
            <p>{filteredFiles.length} of {files.length} files</p>

            <table>
                <thead>
                <tr>
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
                </tr>
                <tr>
                    <th style={{textAlign: 'left'}}>Filename</th>
                    <th style={{textAlign: 'left'}}>Status</th>
                    <th style={{textAlign: 'left'}}>Date</th>
                    <th style={{textAlign: 'left'}}>Template</th>
                </tr>
                </thead>
                <tbody>
                {filteredFiles.map(file => (
                    <Fragment key={file.fileId}>
                        <tr>
                            <td>
                                <a href="#" onClick={(e) => {
                                    e.preventDefault();
                                    toggleInline(file.fileId);
                                }}>
                                    {file.filename}
                                </a>
                            </td>
                            <td>{file.status}</td>
                            <td>{detailsByFileId[file.fileId] ? (detailsByFileId[file.fileId].date ?? '-') : '...'}</td>
                            <td>{detailsByFileId[file.fileId] ? (detailsByFileId[file.fileId].template ?? '-') : '...'}</td>
                        </tr>
                        {inlineFileId === file.fileId && (
                            <tr>
                                <td colSpan={4} style={{padding: '10px', backgroundColor: '#FAFAFA'}}>
                                    {inlineLoading
                                        ? <p>Loading...</p>
                                        : <JsonView src={inlineJson} collapsed={1} theme="default"/>}
                                </td>
                            </tr>
                        )}
                    </Fragment>
                ))}
                </tbody>
            </table>
            </>
            )}
        </div>
    );
}

export default InspectionReportPage;
