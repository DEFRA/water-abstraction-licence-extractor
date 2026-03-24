import React, { useState, useRef, ReactElement } from 'react';
import { Client, LicenceSectionVerification } from '../api/generated/apiClient';

/**
 * Interface that all licence section body components must implement.
 * This allows the parent LicenceSection to control view/edit mode and get edited data.
 */
export interface ILicenceSectionBody {
    /**
     * Returns the current data of the section as JSON.
     */
    getData: () => any;
}

export interface LicenceSectionBodyProps {
    isEditing: boolean;
    onDataChanged?: (data: any) => void;
}

interface LicenceSectionProps {
    title: string;
    children: ReactElement<LicenceSectionBodyProps>;
    initialOpen?: boolean;
    licenceFileId: string;
    processRunId: number;
}

export function LicenceSection({ title, children, initialOpen = false, licenceFileId, processRunId }: LicenceSectionProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);
    const [isEditing, setIsEditing] = useState(false);
    const bodyRef = useRef<ILicenceSectionBody>(null);

    const handleVerification = async (verificationType: string) => {
        if (bodyRef.current) {
            const data = bodyRef.current.getData();
            console.log(`Creating ${verificationType} Verification for`, title, 'Data:', JSON.stringify(data, null, 2));
            
            try {
                const client = new Client();
                const verification = new LicenceSectionVerification({
                    licenceFileId: licenceFileId,
                    processRunId: processRunId,
                    licenceSectionName: title,
                    licenceSectionValue: JSON.stringify(data),
                    verificationType: verificationType
                });

                await client.createLicenceSectionVerification(verification);
            } catch (error) {
                console.error(`Error saving ${verificationType} verification:`, error);
            }
        }
    };

    const handleEditToggle = async () => {
        if (isEditing) {
            await handleVerification('Override');
            setIsEditing(false);
        } else {
            setIsEditing(true);
            setIsOpen(true); // Ensure it's open when editing
        }
    };

    return (
        <div className="licence-section" style={{ border: '1px solid #ccc', marginBottom: '10px', borderRadius: '4px' }}>
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
                <div className="licence-section-actions" onClick={(e) => e.stopPropagation()}>
                    <button onClick={handleEditToggle} style={{ marginRight: '5px' }}>
                        {isEditing ? 'Save Override' : 'Edit'}
                    </button>
                    <button onClick={() => handleVerification('Accept')} style={{ marginRight: '5px' }}>Accept</button>
                    <button onClick={() => handleVerification('Reject')}>Reject</button>
                    <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
                </div>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
                    {React.cloneElement(children, { 
                        isEditing, 
                        ref: bodyRef 
                    } as any)}
                </div>
            )}
        </div>
    );
}
