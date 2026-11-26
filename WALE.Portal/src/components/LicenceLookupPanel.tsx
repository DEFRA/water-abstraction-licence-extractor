import {useState, useEffect} from 'react';
import {LicenceDetailResponse, LicenceSectionVerification} from "../api/generated/apiClient.ts";
import {waleApiClient} from "../api/apiClient";
import {LICENCE_QUEUES} from "../utils/licenceQueues";

interface LicenceLookupPanelProps {
    licenceNumber: string;
    onClear: () => void;
    onLookUp: (licenceNumber: string) => void;
}

function formatDate(value: Date | undefined) {
    return value === undefined ? '--' : new Date(value).toISOString().slice(0, 10);
}

export default function LicenceLookupPanel({licenceNumber, onClear, onLookUp}: LicenceLookupPanelProps) {
    const [detail, setDetail] = useState<LicenceDetailResponse | undefined>(undefined);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;

        waleApiClient.getLicenceDetail(licenceNumber)
            .then(response => {
                if (!cancelled) {
                    setDetail(response);
                    setError(null);
                }
            })
            .catch(err => {
                if (!cancelled) {
                    setError(err instanceof Error ? err.message : 'Failed to look the licence up');
                }
            });

        return () => {
            cancelled = true;
        };
    }, [licenceNumber]);

    const newest = detail?.runs?.[0];

    return (
        <div style={{border: '1px solid #bbb', background: '#f7fbff', padding: '10px', margin: '10px 0'}}>
            <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'baseline'}}>
                <h2 style={{margin: 0, fontSize: '16px'}}>{licenceNumber}</h2>
                <button onClick={onClear} style={{cursor: 'pointer'}}>Close</button>
            </div>

            {error && <div style={{color: '#b00'}}>{error}</div>}
            {!error && detail === undefined && <div>Loading…</div>}

            {detail !== undefined && (
                <div style={{display: 'flex', gap: '28px', flexWrap: 'wrap', marginTop: '8px', fontSize: '13px'}}>
                    <div>
                        <strong>Extracted by</strong>
                        <div>{detail.runs?.length ?? 0} run(s)</div>
                        {newest !== undefined && (
                            <div>
                                newest {newest.processRunId}, {newest.status},{' '}
                                {LICENCE_QUEUES.find(queue => queue.queue === newest.queue)?.label ?? newest.queue}
                            </div>
                        )}
                    </div>

                    <div>
                        <strong>Succeeded by</strong>
                        {(detail.successors?.length ?? 0) === 0
                            ? <div>Nothing in NALD</div>
                            : <div>{detail.successors!.join(' → ')}</div>}
                    </div>

                    <div>
                        <strong>Possibly the same licence</strong>
                        {(detail.possibleVariants?.length ?? 0) === 0
                            ? <div>Nothing similar</div>
                            : detail.possibleVariants!.map((variant: string) => (
                                <div key={variant}>
                                    <a href="#"
                                       onClick={(e) => { e.preventDefault(); onLookUp(variant); }}>{variant}</a>
                                </div>
                            ))}
                    </div>

                    <div>
                        <strong>Verifications</strong>
                        {(detail.verifications?.length ?? 0) === 0
                            ? <div>None recorded</div>
                            : (
                                <table style={{borderCollapse: 'collapse', fontSize: '12px'}}>
                                    <thead>
                                        <tr>
                                            <th style={{textAlign: 'left', padding: '1px 8px'}}>When</th>
                                            <th style={{textAlign: 'left', padding: '1px 8px'}}>Section</th>
                                            <th style={{textAlign: 'left', padding: '1px 8px'}}>Item</th>
                                            <th style={{textAlign: 'left', padding: '1px 8px'}}>Type</th>
                                            <th style={{textAlign: 'left', padding: '1px 8px'}}>Notes</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {detail.verifications!.map((verification: LicenceSectionVerification) => (
                                            <tr key={verification.licenceSectionVerificationId}>
                                                <td style={{padding: '1px 8px'}}>{formatDate(verification.createdDateTimeUtc)}</td>
                                                <td style={{padding: '1px 8px'}}>{verification.licenceSectionName}</td>
                                                <td style={{padding: '1px 8px'}}>{verification.licenceSectionItemId}</td>
                                                <td style={{padding: '1px 8px'}}>{verification.verificationType}</td>
                                                <td style={{padding: '1px 8px'}}>{verification.notes}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            )}
                    </div>
                </div>
            )}
        </div>
    );
}
