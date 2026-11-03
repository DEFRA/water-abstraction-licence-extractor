import {LicenceRunHistoryItem} from "../api/generated/apiClient.ts";
import {LICENCE_QUEUES} from "../utils/licenceQueues";

interface LicenceRunHistoryProps {
    runs: LicenceRunHistoryItem[] | undefined;
    columnCount: number;
}

const Counts: { key: keyof LicenceRunHistoryItem; label: string }[] = [
    {key: 'limitsCount', label: 'Limits'},
    {key: 'pointsCount', label: 'Points'},
    {key: 'purposesCount', label: 'Purposes'},
    {key: 'aggregatesCount', label: 'Aggregates'},
    {key: 'linkedLicencesCount', label: 'Linked'},
    {key: 'licenceSetsCount', label: 'Sets'}
];

/// Bolded only where it moved: a column of identical numbers is what hides the one that did.
function changedFromPrevious(runs: LicenceRunHistoryItem[], index: number, key: keyof LicenceRunHistoryItem) {
    const older = runs[index + 1];

    return older !== undefined && older[key] !== runs[index][key];
}

export default function LicenceRunHistory({runs, columnCount}: LicenceRunHistoryProps) {
    return (
        <tr>
            <td colSpan={columnCount} style={{padding: '6px 10px', background: '#eef3f8'}}>
                {runs === undefined ? 'Loading…' : runs.length === 0 ? 'No runs found.' : (
                    <table style={{borderCollapse: 'collapse', fontSize: '12px'}}>
                        <thead>
                            <tr>
                                <th style={{textAlign: 'left', padding: '2px 8px'}}>Run</th>
                                <th style={{textAlign: 'left', padding: '2px 8px'}}>Queue</th>
                                <th style={{textAlign: 'left', padding: '2px 8px'}}>Status</th>
                                {Counts.map(count => (
                                    <th key={count.key} style={{textAlign: 'right', padding: '2px 8px'}}>
                                        {count.label}
                                    </th>
                                ))}
                                <th style={{textAlign: 'left', padding: '2px 8px'}}>Document</th>
                            </tr>
                        </thead>
                        <tbody>
                            {runs.map((run, index) => (
                                <tr key={run.processRunId}>
                                    <td style={{padding: '2px 8px'}}>{run.processRunId}</td>
                                    <td style={{padding: '2px 8px'}}>
                                        {LICENCE_QUEUES.find(queue => queue.queue === run.queue)?.label ?? run.queue}
                                    </td>
                                    <td style={{padding: '2px 8px'}}>{run.status}</td>
                                    {Counts.map(count => (
                                        <td
                                            key={count.key}
                                            style={{
                                                padding: '2px 8px',
                                                textAlign: 'right',
                                                fontWeight: changedFromPrevious(runs, index, count.key) ? 700 : 400,
                                            }}>
                                            {String(run[count.key])}
                                        </td>
                                    ))}
                                    <td style={{padding: '2px 8px'}}>{run.filename}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </td>
        </tr>
    );
}
