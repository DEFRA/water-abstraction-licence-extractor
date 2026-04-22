import {type ReactElement, useState, useRef, cloneElement} from 'react';
import { LicenceSectionVerification } from '../../api/generated/apiClient';
import { waleApiClient } from '../../api/apiClient';

/**
 * Interface that all licence section body components must implement.
 * This allows the parent LicenceSection to control view/edit mode and get edited data.
 */
export interface ILicenceSectionBody {
    /**
     * Returns the current data of the section as JSON.
     */
    getData: () => any;

    /**
     * Returns the original scraped data of the section as JSON.
     */
    getScrapedData: () => any;
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
    onRefresh?: () => void;
}

export function LicenceSection({ title, children, initialOpen = false, licenceFileId, processRunId, onRefresh }: LicenceSectionProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);
    const [isEditing, setIsEditing] = useState(false);
    const [resetKey, setResetKey] = useState(0);
    const bodyRef = useRef<ILicenceSectionBody>(null);

    const handleVerification = async (verificationType: string) => {
        if (bodyRef.current) {
            const data = bodyRef.current.getData();
            const scrapedData = bodyRef.current.getScrapedData();
            console.log(`Creating ${verificationType} Verification for`, title, 'Data:', JSON.stringify(data, null, 2));
            
            try {
                const verification = new LicenceSectionVerification({
                    licenceFileId: licenceFileId,
                    processRunId: processRunId,
                    licenceSectionName: title,
                    licenceSectionScrapedValue: verificationType === 'Accept' || verificationType === 'Reject' 
                        ? JSON.stringify(data) 
                        : (verificationType === 'Override' ? JSON.stringify(scrapedData) : undefined),
                    licenceSectionOverrideValue: verificationType === 'Override' ? JSON.stringify(data) : undefined,
                    verificationType: verificationType
                });

                await waleApiClient.createLicenceSectionVerification(verification);
                if (onRefresh) {
                    onRefresh();
                }
            } catch (error) {
                console.error(`Error saving ${verificationType} verification:`, error);
            }
        }
    };

    const handleSaveEdit = async () => {
        await handleVerification('Override');
        setIsEditing(false);
    };

    const handleDiscardEdit = async () => {
        setIsEditing(false);
        setResetKey(prev => prev + 1);
    };

    const handleBeginEdit = async () => {
        setIsOpen(true);
        setIsEditing(true);
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
                    {(isEditing &&
                        <>
                        <button onClick={handleSaveEdit} style={{ marginRight: '5px' }}>Save</button>
                        <button onClick={handleDiscardEdit} style={{ marginRight: '5px' }}>Discard</button>
                        </>
                    )}
                    {!isEditing && (
                        <>
                            <button onClick={handleBeginEdit} style={{ marginRight: '5px' }}>Override</button>
                            <button onClick={() => handleVerification('Accept')} style={{ marginRight: '5px' }}>Accept</button>
                            <button onClick={() => handleVerification('Reject')}>Reject</button>
                        </>
                    )}
                    <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
                </div>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
                    {cloneElement(children, { 
                        isEditing, 
                        ref: bodyRef,
                        key: resetKey
                    } as any)}
                </div>
            )}
        </div>
    );
}
