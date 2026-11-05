import type {ReactNode} from 'react';

interface DeleteConfirmModalProps {
    title: ReactNode;
    children: ReactNode;
    isDeleting: boolean;
    onConfirm: () => void;
    onCancel: () => void;
    confirmLabel?: string;
    cancelLabel?: string;
}

export function DeleteConfirmModal({title, children, isDeleting, onConfirm, onCancel, confirmLabel = 'Yes', cancelLabel = 'No'}: DeleteConfirmModalProps) {
    return (
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
                <h4 style={{ marginTop: 0 }}>{title}</h4>
                {children}
                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                    <button disabled={isDeleting} onClick={onCancel}>{cancelLabel}</button>
                    <button
                        disabled={isDeleting}
                        onClick={onConfirm}
                        style={{ backgroundColor: 'red', color: 'white', border: 'none', padding: '5px 15px', borderRadius: '4px', cursor: 'pointer' }}
                    >
                        {confirmLabel}
                    </button>
                </div>
            </div>
        </div>
    );
}
