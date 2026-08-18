import {type ReactElement, useState, useRef, cloneElement} from 'react';
import { LicenceSectionVerification, OutputListDataItem } from '../../api/generated/apiClient';
import { waleApiClient } from '../../api/apiClient';

/**
 * Interface that all licence section body components must implement.
 * This allows the parent LicenceSection to control view/edit mode and get edited data.
 */
export interface VerificationRequestPayload {
    verificationType: string;
    itemId?: string;
    data?: any;
    scrapedData?: any;
    snapshotData?: any;
}

export interface ILicenceSectionBody {
    /**
     * Returns the current data of the section as JSON.
     */
    getData: (itemId?: string) => any;

    /**
     * Returns the original scraped data of the section as JSON.
     */
    getScrapedData: (itemId?: string) => any;

    /**
     * Returns a snapshot of the data of the section, before the current session's edits, as JSON.
     */
    getSnapshotData: (itemId?: string) => any;

    /**
     * Optional override for sections that need custom save behaviour instead of the default single-
     * verification POST (e.g. Aggregates, where editing PrimaryType/SubType/LinkedLicences changes the
     * item's Id and must be saved as Removed(oldId) + Added(newId) rather than a single Edited). If
     * implemented and returns a non-empty array, LicenceSection POSTs each entry in order instead of
     * building one verification from getData/getScrapedData/getSnapshotData. Return null/undefined to
     * fall back to the default single-verification behaviour.
     */
    getVerificationRequests?: (verificationType: string, itemId?: string) => VerificationRequestPayload[] | null;
}

export interface LicenceSectionBodyProps {
    onDataChanged?: (data: any) => void;
    onItemVerificationRequested?: (type: 'Confirm' | 'Remove' | 'Edit' | 'Added' | 'ConfirmNone' | 'RequestBusinessReview' | 'CompleteBusinessReview', itemId?: string) => void;
    onVerificationCancelled?: () => void;
    outputListDataItem?: OutputListDataItem;
    onOpenReport?: (fileId: string) => void;
}

interface LicenceSectionProps {
    title: string;
    itemType?: string;
    children: ReactElement<LicenceSectionBodyProps>;
    initialOpen?: boolean;
    licenceFileId: string;
    processRunId: number;
    onRefresh?: () => void;
    onVerified?: () => void;
    outputListDataItem?: OutputListDataItem;
    onOpenReport?: (fileId: string) => void;
}

export function LicenceSection({ title, itemType, children, initialOpen = false, licenceFileId, processRunId, onRefresh, onVerified, outputListDataItem, onOpenReport }: LicenceSectionProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);
    const [resetKey, setResetKey] = useState(0);
    const bodyRef = useRef<ILicenceSectionBody>(null);

    const [showVerificationPrompt, setShowVerificationPrompt] = useState(false);
    const [pendingVerificationType, setPendingVerificationType] = useState<string | null>(null);
    const [pendingVerificationItemId, setPendingVerificationItemId] = useState<string | undefined>(undefined);
    const [verificationNotes, setVerificationNotes] = useState('');

    const handleVerification = async (verificationType: string, itemId?: string) => {
        setPendingVerificationType(verificationType);
        setPendingVerificationItemId(itemId);
        setVerificationNotes('');
        setShowVerificationPrompt(true);
    };

    const getVerificationActionText = (type: string | null) => {
        switch (type) {
            case 'Edit':
                return 'save changes for';
            case 'Added':
                return 'add';
            case 'RequestBusinessReview':
                return 'request a business review of';
            case 'CompleteBusinessReview':
                return 'complete a business review of';
            default:
                return type?.toLowerCase();
        }
    };

    const confirmVerification = async () => {
        if (!pendingVerificationType) return;
        
        const isConfirmNone = pendingVerificationType === 'ConfirmNone';
        const isBusinessReview = pendingVerificationType === 'RequestBusinessReview' || pendingVerificationType === 'CompleteBusinessReview';
        
        if (bodyRef.current) {
            const data = (isConfirmNone || isBusinessReview) ? undefined : bodyRef.current.getData(pendingVerificationItemId);
            const scrapedData = (isConfirmNone || isBusinessReview) ? undefined : bodyRef.current.getScrapedData(pendingVerificationItemId);
            const snapshotData = (isConfirmNone || isBusinessReview) ? undefined : bodyRef.current.getSnapshotData(pendingVerificationItemId);
            
            // Map the pending verification type to the required verificationType string
            let verificationType: string;
            switch (pendingVerificationType) {
                case 'Confirm':
                case 'ConfirmNone':
                    verificationType = 'Confirmed';
                    break;
                case 'Remove':
                    verificationType = 'Removed';
                    break;
                case 'Edit':
                    verificationType = 'Edited';
                    break;
                case 'Added':
                    verificationType = 'Added';
                    break;
                default:
                    verificationType = pendingVerificationType;
            }

            console.log(`Creating ${verificationType} Verification for`, title, 'Item:', pendingVerificationItemId, 'Data:', JSON.stringify(data, null, 2));

            try {
                const overrideRequests = bodyRef.current.getVerificationRequests?.(verificationType, pendingVerificationItemId);

                if (overrideRequests && overrideRequests.length > 0) {
                    for (const req of overrideRequests) {
                        const verification = new LicenceSectionVerification({
                            licenceFileId: licenceFileId,
                            processRunId: processRunId,
                            licenceSectionName: title,
                            licenceSectionScrapedValue: req.scrapedData ? JSON.stringify(req.scrapedData) : undefined,
                            licenceSectionSnapshotValue: req.verificationType === 'Added' ? undefined : (req.snapshotData ? JSON.stringify(req.snapshotData) : undefined),
                            licenceSectionOverrideValue: (req.verificationType === 'Edited' || req.verificationType === 'Added') ? JSON.stringify(req.data) : undefined,
                            verificationType: req.verificationType,
                            licenceSectionItemId: req.itemId,
                            notes: verificationNotes
                        });

                        await waleApiClient.createLicenceSectionVerification(verification);
                    }
                } else {
                    const verification = new LicenceSectionVerification({
                        licenceFileId: licenceFileId,
                        processRunId: processRunId,
                        licenceSectionName: title,
                        licenceSectionScrapedValue: scrapedData ? JSON.stringify(scrapedData) : undefined,
                        licenceSectionSnapshotValue: (verificationType === 'Added' || isConfirmNone || isBusinessReview) ? undefined : JSON.stringify(snapshotData),
                        licenceSectionOverrideValue: ((verificationType === 'Edited' || verificationType === 'Added') && !isConfirmNone && !isBusinessReview) ? JSON.stringify(data) : undefined,
                        verificationType: verificationType,
                        licenceSectionItemId: pendingVerificationItemId,
                        notes: verificationNotes
                    });

                    await waleApiClient.createLicenceSectionVerification(verification);
                }

                setResetKey(prev => prev + 1);
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
        setPendingVerificationItemId(undefined);
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
                    <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
                </div>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
                    {cloneElement(children, { 
                        ref: bodyRef,
                        key: resetKey,
                        onItemVerificationRequested: (type: 'Confirm' | 'Remove' | 'Edit' | 'Added' | 'ConfirmNone' | 'RequestBusinessReview' | 'CompleteBusinessReview', itemId?: string) => handleVerification(type, itemId),
                        onVerificationCancelled: () => {
                            setPendingVerificationType(null);
                            setPendingVerificationItemId(undefined);
                            setShowVerificationPrompt(false);
                        },
                        outputListDataItem: outputListDataItem,
                        onOpenReport: onOpenReport
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
                        <p>
                            {pendingVerificationType === 'ConfirmNone' ? (
                                `Are you sure you want to confirm there are no ${title.toLowerCase()} for this licence?`
                            ) : (
                                <>
                                    Are you sure you want to {getVerificationActionText(pendingVerificationType)} 
                                    {pendingVerificationItemId ? ` the ${itemType || (title.endsWith('s') ? title.slice(0, -1) : title)} ${pendingVerificationItemId}` : ` the ${title}`} for this licence?
                                </>
                            )}
                        </p>
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
                            <button onClick={() => { 
                                setShowVerificationPrompt(false); 
                                setPendingVerificationType(null); 
                                if (bodyRef.current && (bodyRef.current as any).onVerificationCancelled) {
                                    (bodyRef.current as any).onVerificationCancelled();
                                }
                            }}>No</button>
                            <button onClick={confirmVerification} style={{ backgroundColor: '#007bff', color: 'white', border: 'none', padding: '5px 15px', borderRadius: '4px', cursor: 'pointer' }}>Yes</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
