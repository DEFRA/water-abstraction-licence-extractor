import {useEffect, useState} from 'react';
import {
    LicenceRunComparison,
    LicenceRunDiffLineKind,
    LicenceRunHistoryItem
} from "../api/generated/apiClient.ts";
import {waleApiClient} from "../api/apiClient";

interface LicenceRunDiffModalProps {
    licenceNumber: string;
    newerRunId: number;
    olderRuns: LicenceRunHistoryItem[];
    onClose: () => void;
}

const LineColours: Record<string, { background: string; marker: string }> = {
    [LicenceRunDiffLineKind.Removed]: {background: '#ffeef0', marker: '-'},
    [LicenceRunDiffLineKind.Added]: {background: '#e6ffed', marker: '+'},
    [LicenceRunDiffLineKind.Context]: {background: 'transparent', marker: ' '}
};

function runDate(run: LicenceRunHistoryItem) {
    return run.createdDateTimeUtc === undefined
        ? ''
        : new Date(run.createdDateTimeUtc).toISOString().slice(0, 10);
}

export default function LicenceRunDiffModal(
    {licenceNumber, newerRunId, olderRuns, onClose}: LicenceRunDiffModalProps) {
    const [olderRunId, setOlderRunId] = useState(olderRuns[0]?.processRunId ?? 0);
    const [loaded, setLoaded] = useState<LicenceRunComparison | undefined>(undefined);

    /// Held with the run it answers for, so switching runs reads as loading without the
    /// effect having to clear it first.
    const comparison = loaded?.olderRunId === olderRunId && loaded?.newerRunId === newerRunId
        ? loaded
        : undefined;

    useEffect(() => {
        let cancelled = false;

        waleApiClient.getLicenceRunComparison(licenceNumber, newerRunId, olderRunId)
            .then(result => {
                if (!cancelled) {
                    setLoaded(result);
                }
            });

        return () => {
            cancelled = true;
        };
    }, [licenceNumber, newerRunId, olderRunId]);

    /// Escape closes it, since the diff covers the table it was opened from.
    useEffect(() => {
        const close = (e: KeyboardEvent) => {
            if (e.key === 'Escape') {
                onClose();
            }
        };

        window.addEventListener('keydown', close);

        return () => window.removeEventListener('keydown', close);
    }, [onClose]);

    return (
        <div
            onClick={onClose}
            style={{
                position: 'fixed',
                inset: 0,
                background: 'rgba(0, 0, 0, 0.45)',
                zIndex: 2000,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center'
            }}>
            <div
                onClick={(e) => e.stopPropagation()}
                style={{
                    background: '#fff',
                    width: 'min(1100px, 92vw)',
                    height: '88vh',
                    display: 'flex',
                    flexDirection: 'column',
                    border: '1px solid #888'
                }}>
                <div style={{
                    display: 'flex',
                    alignItems: 'baseline',
                    gap: '12px',
                    padding: '8px 12px',
                    borderBottom: '1px solid #ddd'
                }}>
                    <strong>{licenceNumber}</strong>

                    <span>
                        run {newerRunId} against{' '}
                        <select
                            value={olderRunId}
                            onChange={(e) => setOlderRunId(Number(e.target.value))}>
                            {olderRuns.map(run => (
                                <option key={run.processRunId} value={run.processRunId}>
                                    run {run.processRunId} ({runDate(run)})
                                </option>
                            ))}
                        </select>
                    </span>

                    {comparison !== undefined && (
                        <span style={{color: '#666', fontSize: '12px'}}>
                            {comparison.differences?.length ?? 0} field(s) changed
                        </span>
                    )}

                    <button onClick={onClose} style={{marginLeft: 'auto', cursor: 'pointer'}}>Close</button>
                </div>

                <div style={{flex: 1, overflow: 'auto', fontSize: '12px'}}>
                    <LicenceRunDiffBody comparison={comparison}/>
                </div>
            </div>
        </div>
    );
}

function LicenceRunDiffBody({comparison}: { comparison: LicenceRunComparison | undefined }) {
    if (comparison === undefined) {
        return <div style={{padding: '12px'}}>Comparing…</div>;
    }

    if (!comparison.newerRunHasData || !comparison.olderRunHasData) {
        const missing = [
            comparison.newerRunHasData ? undefined : comparison.newerRunId,
            comparison.olderRunHasData ? undefined : comparison.olderRunId
        ].filter(run => run !== undefined);

        return (
            <div style={{padding: '12px'}}>
                No extraction stored for run {missing.join(' or ')}, so there is nothing to compare.
            </div>
        );
    }

    if (comparison.identical) {
        return (
            <div style={{padding: '12px'}}>
                Runs {comparison.olderRunId} and {comparison.newerRunId} extracted the same
                thing - only the run and result ids differ.
            </div>
        );
    }

    return (
        <table style={{
            borderCollapse: 'collapse',
            width: '100%',
            fontFamily: 'ui-monospace, Menlo, Consolas, monospace'
        }}>
            <tbody>
                {comparison.lines?.map((line, index) => {
                    if (line.kind === LicenceRunDiffLineKind.Skipped) {
                        return (
                            <tr key={index} style={{background: '#f1f5f9', color: '#64748b'}}>
                                <td colSpan={3} style={{padding: '2px 8px'}}>⋯ {line.text}</td>
                            </tr>
                        );
                    }

                    const colours = LineColours[line.kind ?? LicenceRunDiffLineKind.Context];

                    return (
                        <tr key={index} style={{background: colours.background}}>
                            <td style={{
                                padding: '0 6px',
                                textAlign: 'right',
                                color: '#94a3b8',
                                width: '44px',
                                userSelect: 'none'
                            }}>
                                {line.olderLineNumber}
                            </td>
                            <td style={{
                                padding: '0 6px',
                                textAlign: 'right',
                                color: '#94a3b8',
                                width: '44px',
                                userSelect: 'none'
                            }}>
                                {line.newerLineNumber}
                            </td>
                            <td style={{padding: '0 8px', whiteSpace: 'pre-wrap'}}>
                                {colours.marker}{line.text}
                            </td>
                        </tr>
                    );
                })}
            </tbody>
        </table>
    );
}
