import {type ReactNode, useState } from 'react';
import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";

interface LicenceSectionVerificationHistoryProps {
    verification: LicenceSectionVerification;
    children: ReactNode;
    initialOpen?: boolean;
}

export function LicenceSectionVerificationHistory({ verification, children, initialOpen = false }: LicenceSectionVerificationHistoryProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);

    const sectionName = verification.licenceSectionName || 'N/A';
    const verificationType = verification.verificationType || 'N/A';
    const notes = verification.notes;
    const date = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleDateString() : 'N/A';
    const dateTime = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A';

    const getVerificationTypeColor = (type: string) => {
        switch (type.toLowerCase()) {
            case 'reject':
                return 'red';
            case 'accept':
                return 'green';
            case 'override':
                return 'blue';
            default:
                return 'inherit';
        }
    };

    return (
        <div className="licence-section-verification-history" style={{ border: '1px solid #ccc', marginBottom: '10px', borderRadius: '4px' }}>
            <div 
                className="licence-section-header" 
                style={{ 
                    padding: '10px', 
                    backgroundColor: '#f5f5f5', 
                    cursor: 'pointer', 
                    display: 'flex', 
                    justifyContent: 'space-between',
                    alignItems: 'center'
                }}
                onClick={() => setIsOpen(!isOpen)}
            >
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>
                    {sectionName} - <span style={{ color: getVerificationTypeColor(verificationType) }}>{verificationType}</span> - {date}
                </h3>
                <div className="licence-section-actions">
                    <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
                </div>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
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
                </div>
            )}
        </div>
    );
}
