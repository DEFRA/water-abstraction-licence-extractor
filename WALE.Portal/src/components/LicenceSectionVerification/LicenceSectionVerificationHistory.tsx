import {type ReactNode, useState } from 'react';
import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";
import { getVerificationTypeColor } from "../../utils/verificationUtils.ts";
import { CollapsibleItem } from './CollapsibleItem';
import { waleApiClient } from "../../api/apiClient.ts";

interface LicenceSectionVerificationHistoryProps {
    verification: LicenceSectionVerification;
    children: ReactNode;
    initialOpen?: boolean;
    canDelete?: boolean;
    onRefresh?: () => void;
    onDeleted?: () => void;
}

export function LicenceSectionVerificationHistory({ verification, children, initialOpen = false, canDelete = false, onRefresh, onDeleted }: LicenceSectionVerificationHistoryProps) {
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);

    const sectionName = verification.licenceSectionName || 'N/A';
    const verificationType = verification.verificationType || 'N/A';
    const notes = verification.notes;
    const date = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleDateString() : 'N/A';
    const dateTime = verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A';
    const itemId = verification.licenceSectionItemId;

    const isDeleted = !!verification.deletedDateTimeUtc;
    const deletedLabel = isDeleted ? `Deleted ${new Date(verification.deletedDateTimeUtc!).toLocaleString()}` : null;

    const handleDelete = async () => {
        setIsDeleting(true);
        try {
            await waleApiClient.deleteLicenceSectionVerification(verification);
            setShowDeleteConfirm(false);
            onDeleted?.();
            onRefresh?.();
        } catch (error) {
            console.error('Error deleting verification:', error);
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <>
            <CollapsibleItem
                variant="section"
                defaultOpen={initialOpen}
                summary={
                    <h3 style={{ margin: 0, fontSize: '1.1rem' }}>
                        <span style={isDeleted ? { textDecoration: 'line-through' } : undefined}>
                            {sectionName} - <span style={{ color: getVerificationTypeColor(verificationType) }}>{verificationType}</span>{itemId && (<> -  {itemId}</>)} - {date}
                        </span>
                        {isDeleted && (
                            <span style={{ marginLeft: '8px', color: '#666', fontStyle: 'italic' }}> {deletedLabel}</span>
                        )}
                    </h3>
                }
            >
                <div style={{ position: 'relative' }}>
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
                    {canDelete && !isDeleted && (
                        <div style={{ marginTop: '10px', textAlign: 'right' }}>
                            <button
                                onClick={() => setShowDeleteConfirm(true)}
                                style={{ padding: '4px 12px', backgroundColor: 'red', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem' }}
                            >
                                Delete
                            </button>
                        </div>
                    )}
                    {isDeleted && (
                        <div style={{ position: 'absolute', inset: 0, backgroundColor: 'rgba(128,128,128,0.6)', pointerEvents: 'none' }} />
                    )}
                </div>
            </CollapsibleItem>
            {showDeleteConfirm && (
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
                        <h4 style={{ marginTop: 0 }}>Delete Verification</h4>
                        <p>Are you sure you want to delete this verification?</p>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                            <button disabled={isDeleting} onClick={() => setShowDeleteConfirm(false)}>No</button>
                            <button
                                disabled={isDeleting}
                                onClick={handleDelete}
                                style={{ backgroundColor: 'red', color: 'white', border: 'none', padding: '5px 15px', borderRadius: '4px', cursor: 'pointer' }}
                            >
                                Yes
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}
