import {type ReactNode, useState } from 'react';

interface LicenceSectionVerificationHistoryProps {
    title: string;
    children: ReactNode;
    initialOpen?: boolean;
}

export function LicenceSectionVerificationHistory({ title, children, initialOpen = false }: LicenceSectionVerificationHistoryProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);

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
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>{title}</h3>
                <div className="licence-section-actions">
                    <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
                </div>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
                    {children}
                </div>
            )}
        </div>
    );
}
