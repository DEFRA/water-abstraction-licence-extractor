import {type ReactNode } from 'react';
import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";
import { getVerificationTypeColor } from "../../utils/verificationUtils.ts";
import { CollapsibleItem } from './CollapsibleItem';

interface LicenceSectionVerificationHistoryProps {
    verification: LicenceSectionVerification;
    children: ReactNode;
    initialOpen?: boolean;
}

export function LicenceSectionVerificationHistory({ verification, children, initialOpen = false }: LicenceSectionVerificationHistoryProps) {
    const sectionName = verification.licenceSectionName || 'N/A';
    const verificationType = verification.verificationType || 'N/A';
    const notes = verification.notes;
    const date = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleDateString() : 'N/A';
    const dateTime = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A';
    const itemId = verification.licenceSectionItemId;

    return (
        <CollapsibleItem
            variant="section"
            defaultOpen={initialOpen}
            summary={
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>
                    {sectionName} - <span style={{ color: getVerificationTypeColor(verificationType) }}>{verificationType}</span>{itemId && (<> -  {itemId}</>)} - {date}
                </h3>
            }
        >
            {children}
            {verification.processRunId && (
                <div
                    className="licence-section-footer"
                    style={{
                        marginTop: '10px',
                        paddingTop: '5px',
                        borderTop: '1px dashed #eee',
                        fontSize: '0.8rem',
                        color: '#666',
                        textAlign: 'right'
                    }}
                >
                    {notes && (<span>Notes: {notes}<br/></span>)}
                    Verified at: {dateTime}<br/>
                    Verified against process run: {verification.processRunId}<br/>
                </div>
            )}
        </CollapsibleItem>
    );
}
