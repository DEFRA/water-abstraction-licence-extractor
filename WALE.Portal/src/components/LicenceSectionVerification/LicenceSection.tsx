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
    onVerified?: () => void;
}

export function LicenceSection({ title, children, initialOpen = false, licenceFileId, processRunId, onRefresh, onVerified }: LicenceSectionProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);
    const [isEditing, setIsEditing] = useState(false);
    const [resetKey, setResetKey] = useState(0);
    const bodyRef = useRef<ILicenceSectionBody>(null);

    const [showVerificationPrompt, setShowVerificationPrompt] = useState(false);
    const [pendingVerificationType, setPendingVerificationType] = useState<string | null>(null);
    const [verificationNotes, setVerificationNotes] = useState('');

    const handleVerification = async (verificationType: string) => {
        setPendingVerificationType(verificationType);
        setVerificationNotes('');
        setShowVerificationPrompt(true);
    };

    const confirmVerification = async () => {
        if (!pendingVerificationType) return;
        
        if (bodyRef.current) {
            const data = bodyRef.current.getData();
            const scrapedData = bodyRef.current.getScrapedData();
            console.log(`Creating ${pendingVerificationType} Verification for`, title, 'Data:', JSON.stringify(data, null, 2));
            
            try {
                const verification = new LicenceSectionVerification({
                    licenceFileId: licenceFileId,
                    processRunId: processRunId,
                    licenceSectionName: title,
                    licenceSectionScrapedValue: JSON.stringify(scrapedData),
                    licenceSectionOverrideValue: pendingVerificationType === 'Override' ? JSON.stringify(data) : undefined,
                    verificationType: pendingVerificationType,
                    notes: verificationNotes
                });

                await waleApiClient.createLicenceSectionVerification(verification);
                if (onRefresh) {
                    onRefresh();
                }
                if (onVerified) {
                    onVerified();
                }
            } catch (error) {
                console.error(`Error saving ${pendingVerificationType} verification:`, error);
            }
        }
        setShowVerificationPrompt(false);
        setPendingVerificationType(null);
    };

    const handleSaveEdit = async () => {
        setPendingVerificationType('Override');
        setVerificationNotes('');
        setShowVerificationPrompt(true);
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
                    {(isOpen && isEditing &&
                        <>
                        <button onClick={handleSaveEdit} style={{ marginRight: '5px' }}>Save</button>
                        <button onClick={handleDiscardEdit} style={{ marginRight: '5px' }}>Discard</button>
                        </>
                    )}
                    {isOpen && !isEditing && (
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
            {showVerificationPrompt && (
                <div style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0,0,0,0.5)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 1000
                }}>
                    <div style={{
                        backgroundColor: 'white',
                        padding: '20px',
                        borderRadius: '8px',
                        maxWidth: '500px',
                        width: '100%',
                        boxShadow: '0 2px 10px rgba(0,0,0,0.1)'
                    }}>
                        <h4 style={{ marginTop: 0 }}>Verification Confirmation</h4>
                        <p>Are you sure you want to {pendingVerificationType?.toLowerCase()} the {title} for this licence?</p>
                        <div style={{ marginBottom: '15px' }}>
                            <label htmlFor="verificationNotes" style={{ display: 'block', marginBottom: '5px' }}>Notes:</label>
                            <textarea
                                id="verificationNotes"
                                value={verificationNotes}
                                onChange={(e) => setVerificationNotes(e.target.value)}
                                style={{ width: '100%', minHeight: '80px', padding: '8px', boxSizing: 'border-box' }}
                                placeholder="Enter notes here..."
                            />
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                            <button onClick={() => { setShowVerificationPrompt(false); setPendingVerificationType(null); }}>No</button>
                            <button onClick={confirmVerification} style={{ backgroundColor: '#007bff', color: 'white', border: 'none', padding: '5px 15px', borderRadius: '4px', cursor: 'pointer' }}>Yes</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
