import {useState} from 'react';
import {waleApiBaseUrl} from '../api/apiClient';

interface ExportLatestReportsProps {
    template: string;
}

/// Four files rather than one because the flattened export keeps a document's first meter and drops
/// the rest; the per-meter and per-action files carry the repeating groups, joined on the file name.
export function ExportLatestReports({template}: ExportLatestReportsProps) {
    const [from, setFrom] = useState('');
    const [to, setTo] = useState('');
    const [excludeInternalColumns, setExcludeInternalColumns] = useState(true);

    const base = waleApiBaseUrl.replace(/\/$/, '');

    /// Omitted rather than sent empty: either bound given drops the documents with no date at all.
    const query = (withColumns: boolean) => {
        const parts = [
            from && `from=${from}`,
            to && `to=${to}`,
            template && `template=${template}`,
            withColumns && `excludeInternalColumns=${excludeInternalColumns}`,
        ].filter(Boolean);

        return parts.length > 0 ? `?${parts.join('&')}` : '';
    };

    const link = (action: string, label: string, withColumns: boolean) => (
        <a
            href={`${base}/BFF/FileData/${action}${query(withColumns)}`}
            style={{marginRight: '12px'}}>
            {label}
        </a>
    );

    return (
        <div style={{
            border: '1px solid #c3e6cb', color: '#155724', padding: '10px',
            margin: '10px 0', borderRadius: '4px', fontSize: '12px',
        }}>
            <p style={{marginTop: 0}}>
                Export the newest extraction of every document{template && <> on template <strong>{template}</strong></>}.
                Leave the dates empty for everything, including documents with no date.
            </p>

            <div style={{display: 'flex', gap: '12px', alignItems: 'center', marginBottom: '8px'}}>
                <label>
                    From{' '}
                    <input type="date" value={from} onChange={event => setFrom(event.target.value)}/>
                </label>
                <label>
                    To{' '}
                    <input type="date" value={to} onChange={event => setTo(event.target.value)}/>
                </label>
                <label>
                    <input
                        type="checkbox"
                        checked={excludeInternalColumns}
                        onChange={event => setExcludeInternalColumns(event.target.checked)}
                    />
                    {' '}Exclude internal columns (including the full extracted JSON)
                </label>
            </div>

            <div>
                {link('ExportWrInspectionReportLatestCsv', 'Reports (CSV)', true)}
                {link('ExportWrInspectionReportLatestXlsx', 'Reports (XLSX)', true)}
                {link('ExportWrInspectionReportLatestMetersCsv', 'One row per meter (CSV)', false)}
                {link('ExportWrInspectionReportLatestActionsCsv', 'One row per action (CSV)', false)}
            </div>
        </div>
    );
}

export default ExportLatestReports;
