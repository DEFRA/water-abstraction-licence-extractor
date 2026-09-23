import {useState} from 'react';
import {waleApiBaseUrl} from '../api/apiClient';

interface ExportReportProps {
    processRunId: number;
}

export function ExportReport({processRunId}: ExportReportProps) {
    const [excludeInternalColumns, setExcludeInternalColumns] = useState(true);

    const base = waleApiBaseUrl.replace(/\/$/, '');
    const query = `processRunId=${processRunId}&excludeInternalColumns=${excludeInternalColumns}`;
    const csvUrl = `${base}/BFF/FileData/ExportWrInspectionReportCsv?${query}`;
    const xlsxUrl = `${base}/BFF/FileData/ExportWrInspectionReportXlsx?${query}`;

    return (
        <div
            style={{
                border: '1px solid #c3e6cb',
                color: '#155724',
                padding: '10px',
                marginBottom: '10px',
                marginTop: '20px',
                borderRadius: '4px'
            }}
        >
            <p style={{marginTop: 0}}>Export this process run's results as a single file.</p>
            <label style={{display: 'block', marginBottom: '8px'}}>
                <input
                    type="checkbox"
                    checked={excludeInternalColumns}
                    onChange={(e) => setExcludeInternalColumns(e.target.checked)}
                />
                {' '}Exclude internal/debug columns (Images, raw date/time fields, template version)
            </label>
            <a href={csvUrl}>
                <button type="button">Download CSV</button>
            </a>
            &nbsp;
            <a href={xlsxUrl}>
                <button type="button">Download XLSX</button>
            </a>
        </div>
    );
}
