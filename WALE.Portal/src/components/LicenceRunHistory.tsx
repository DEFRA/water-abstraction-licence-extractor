import {Fragment, useState} from 'react';
import {LicenceRunHistoryItem} from "../api/generated/apiClient.ts";
import LicenceRunDiffModal from "./LicenceRunDiffModal";
import {LICENCE_QUEUES} from "../utils/licenceQueues";

interface LicenceRunHistoryProps {
    licenceNumber: string;
    runs: LicenceRunHistoryItem[] | undefined;
    columnCount: number;
    onOpenRunReport: (run: LicenceRunHistoryItem) => void;
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

function naldRevision(run: LicenceRunHistoryItem) {
    if (run.naldIssueNumber === undefined || run.naldIssueNumber === null) {
        return '--';
    }

    return `${run.naldIssueNumber}/${run.naldIncrementNumber ?? 0}`;
}

function runDate(run: LicenceRunHistoryItem) {
    return run.createdDateTimeUtc === undefined
        ? '--'
        : new Date(run.createdDateTimeUtc).toISOString().slice(0, 10);
}

const headerCell = {textAlign: 'left', padding: '2px 8px'} as const;
const cell = {padding: '2px 8px'};

export default function LicenceRunHistory({licenceNumber, runs, columnCount, onOpenRunReport}: LicenceRunHistoryProps) {
    const [comparing, setComparing] = useState<number | undefined>(undefined);

    if (runs === undefined || runs.length === 0) {
        return (
            <tr>
                <td colSpan={columnCount} style={{padding: '6px 10px', background: '#eef3f8'}}>
                    {runs === undefined ? 'Loading…' : 'No runs found.'}
                </td>
            </tr>
        );
    }

    /// 33 of 2,160 licences map to more than one document, so for every other licence this was
    /// the widest column in the table carrying the same value on every row.
    const documents = [...new Set(runs.map(run => run.filename))];

    return (
        <tr>
            <td colSpan={columnCount} style={{padding: '6px 10px', background: '#eef3f8'}}>
                {documents.length === 1 && (
                    <div style={{fontSize: '12px', marginBottom: '4px'}}>
                        <strong>Document:</strong> {documents[0]}
                    </div>
                )}

                <table style={{borderCollapse: 'collapse', fontSize: '12px'}}>
                    <thead>
                        <tr>
                            <th style={headerCell}>Run</th>
                            <th style={headerCell}>Date</th>
                            <th style={headerCell}>Queue</th>
                            <th style={headerCell}>Status</th>
                            <th style={headerCell}>NALD doc</th>
                            <th style={headerCell}>Rev</th>
                            {Counts.map(count => (
                                <th key={count.key} style={{textAlign: 'right', padding: '2px 8px'}}>
                                    {count.label}
                                </th>
                            ))}
                            {documents.length > 1 && <th style={headerCell}>Document</th>}
                            <th style={headerCell}></th>
                        </tr>
                    </thead>
                    <tbody>
                        {runs.map((run, index) => {
                            const older = runs[index + 1];
                            const runId = run.processRunId ?? 0;
                            const statusMoved = changedFromPrevious(runs, index, 'status');

                            return (
                                <Fragment key={runId}>
                                    <tr>
                                        <td style={cell}>
                                            <a href="#"
                                               title="This run's extraction"
                                               onClick={(e) => {
                                                   e.preventDefault();
                                                   onOpenRunReport(run);
                                               }}>{runId}</a>
                                            <span
                                                title={`This run projected ${run.runLicenceCount} licences`}
                                                style={{color: '#777'}}> ({run.runLicenceCount})</span>
                                        </td>
                                        <td style={cell}>{runDate(run)}</td>
                                        <td style={cell}>
                                            {LICENCE_QUEUES.find(queue => queue.queue === run.queue)?.label ?? run.queue}
                                        </td>
                                        <td style={{...cell, fontWeight: statusMoved ? 700 : 400}}>
                                            {index === runs.length - 1 || statusMoved ? run.status : ''}
                                        </td>
                                        <td style={{
                                            ...cell,
                                            fontWeight: changedFromPrevious(runs, index, 'licenceVersionId') ? 700 : 400
                                        }}>
                                            {run.licenceVersionId ?? '--'}
                                        </td>
                                        <td style={{
                                            ...cell,
                                            fontWeight: changedFromPrevious(runs, index, 'naldIssueNumber')
                                                || changedFromPrevious(runs, index, 'naldIncrementNumber') ? 700 : 400
                                        }}
                                            title={run.naldUpdateReason ?? undefined}>
                                            {naldRevision(run)}
                                        </td>
                                        {Counts.map(count => (
                                            <td
                                                key={count.key}
                                                style={{
                                                    ...cell,
                                                    textAlign: 'right',
                                                    fontWeight: changedFromPrevious(runs, index, count.key) ? 700 : 400,
                                                }}>
                                                {String(run[count.key])}
                                            </td>
                                        ))}
                                        {documents.length > 1 && (
                                            <td style={{
                                                ...cell,
                                                fontWeight: changedFromPrevious(runs, index, 'filename') ? 700 : 400
                                            }}>
                                                {run.filename}
                                            </td>
                                        )}
                                        <td style={cell}>
                                            {older !== undefined && run.changedFromPrevious === true && (
                                                <button
                                                    onClick={() => setComparing(runId)}
                                                    title={`What changed between run ${older.processRunId} and run ${runId}`}
                                                    style={{cursor: 'pointer', fontSize: '11px', padding: '0 5px'}}>
                                                    vs {older.processRunId}
                                                </button>
                                            )}
                                            {older !== undefined && run.changedFromPrevious === false && (
                                                <span
                                                    title={`Same extraction as run ${older.processRunId}`}
                                                    style={{color: '#999'}}>=</span>
                                            )}
                                        </td>
                                    </tr>

                                </Fragment>
                            );
                        })}
                    </tbody>
                </table>

                {comparing !== undefined && (
                    <LicenceRunDiffModal
                        licenceNumber={licenceNumber}
                        newerRunId={comparing}
                        olderRuns={runs.slice(runs.findIndex(run => run.processRunId === comparing) + 1)}
                        onClose={() => setComparing(undefined)}
                    />
                )}
            </td>
        </tr>
    );
}
