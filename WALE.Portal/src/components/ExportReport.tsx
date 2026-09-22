import {useState} from 'react';
import {waleApiBaseUrl} from '../api/apiClient';

interface ExportReportProps {
    processRunId: number;
}

// Plain links straight at the BFF export endpoints rather than the generated NSwag client -
// these are simple no-auth GETs that return a file with Content-Disposition: attachment already
// set server-side (see FileDataController.ExportWrInspectionReportCsv/Xlsx), so the browser's
// native download behaviour is all that's needed; no client regeneration required to add this.
export function ExportReport({processRunId}: ExportReportProps) {
    // Defaults to excluded - internal/derived-only columns (Images, the Raw* duplicates,
    // InspectionDate__Year, Metadata__DocumentTemplateVerison) roughly double the export's size
    // for little report value; still available via the checkbox for debugging a specific file.
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
