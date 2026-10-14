import {type ReactNode, useState} from 'react';
import {LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {getVerificationTypeColor} from "../../utils/verificationUtils.ts";
import {waleApiClient} from "../../api/apiClient.ts";
import {CollapsibleItem} from './CollapsibleItem';
import {DeleteConfirmModal} from './DeleteConfirmModal';

interface ItemVerificationHistoryGroupProps {
    itemId: string;
    sectionName: string;
    verifications: LicenceSectionVerification[];
    defaultOpen: boolean;
    children: ReactNode;
    onRefresh?: () => void;
    onDeleted?: () => void;
}

export function ItemVerificationHistoryGroup({itemId, sectionName, verifications, defaultOpen, children, onRefresh, onDeleted}: ItemVerificationHistoryGroupProps) {
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);

    const activeVerifications = verifications.filter(v => !v.deletedDateTimeUtc);
    const activeCount = activeVerifications.length;
    const entriesLabel = `${activeCount} ${activeCount === 1 ? 'entry' : 'entries'}`;

    const handleDelete = async () => {
        setIsDeleting(true);
        try {
            await waleApiClient.deleteLicenceSectionVerifications(activeVerifications);
            setShowDeleteConfirm(false);
            onDeleted?.();
            onRefresh?.();
        } catch (error) {
            console.error('Error deleting verification history:', error);
        } finally {
            setIsDeleting(false);
        }
    };

    return (
        <>
            <CollapsibleItem
                variant="item"
                defaultOpen={defaultOpen}
                summary={
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '10px'}}>
                        <h4 style={{ margin: 0, fontSize: '1.05rem', textDecoration: activeCount === 0 ? 'line-through' : undefined }}>{itemId}</h4>
                        {activeCount > 0 && (
                            <button
                                onClick={e => {
                                    e.stopPropagation();
                                    setShowDeleteConfirm(true);
                                }}
                                style={{ padding: '4px 12px', backgroundColor: 'red', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem', whiteSpace: 'nowrap' }}
                            >
                                Delete all ({activeCount})
                            </button>
                        )}
                        {activeCount === 0 && (
                            <span style={{ marginLeft: '8px', color: 'rgb(102, 102, 102)', fontStyle: 'italic' }}>
                                All deleted
                            </span>
                        )}
                    </div>
                }
            >
                {children}
            </CollapsibleItem>
            {showDeleteConfirm && (
                <DeleteConfirmModal
                    title={`Delete entire verification history for ${itemId}`}
                    isDeleting={isDeleting}
                    onConfirm={handleDelete}
                    onCancel={() => setShowDeleteConfirm(false)}
                    confirmLabel={`Delete ${entriesLabel}`}
                    cancelLabel="Cancel"
                >
                    <p>
                        This will delete <strong>all ({activeCount}) verification {activeCount === 1 ? 'entry' : 'entries'}</strong> for {sectionName} <strong>{itemId}</strong>
                    </p>
                    <ul style={{ maxHeight: '200px', overflowY: 'auto', border: '1px solid #eee', borderRadius: '4px', padding: '8px 8px 8px 28px', margin: '0 0 10px 0' }}>
                        {activeVerifications.map((v, index) => {
                            const verificationType = v.verificationType || 'N/A';
                            const date = v.createdDateTimeUtc ? new Date(v.createdDateTimeUtc).toLocaleDateString() : 'N/A';
                            return (
                                <li key={v.licenceSectionVerificationId || index}>
                                    <span style={{ color: getVerificationTypeColor(verificationType) }}>{verificationType}</span>{v.processRunId && (<> - PR {v.processRunId}</>)} - {date}
                                </li>
                            );
                        })}
                    </ul>
                </DeleteConfirmModal>
            )}
        </>
    );
}
