import React, { useState, useImperativeHandle, forwardRef, useEffect } from 'react';
import type { Licence } from "../api/generated/apiClient.ts";
import { ILicenceSectionBody, LicenceSectionBodyProps } from "./LicenceSection.tsx";

interface LinkedLicencesProps extends LicenceSectionBodyProps {
    licence: Licence;
}

export const LinkedLicences = forwardRef<ILicenceSectionBody, LinkedLicencesProps>(
    ({ licence, isEditing }, ref) => {
        const [editableLicenceNumber, setEditableLicenceNumber] = useState(licence.licenceNumber?.value || '');

        // Expose data to parent via ref
        useImperativeHandle(ref, () => ({
            getData: () => ({
                licenceNumber: editableLicenceNumber,
                // Add more fields here as needed
            })
        }));

        // Reset or update editable state when licence changes
        useEffect(() => {
            setEditableLicenceNumber(licence.licenceNumber?.value || '');
        }, [licence]);

        if (isEditing) {
            return (
                <div className="linked-licences-edit">
                    <label>
                        Licence Number:
                        <input 
                            type="text" 
                            value={editableLicenceNumber} 
                            onChange={(e) => setEditableLicenceNumber(e.target.value)} 
                        />
                    </label>
                    <p style={{ fontStyle: 'italic', fontSize: '0.9rem' }}>Editing Linked Licences...</p>
                </div>
            );
        }

        return (
            <div className="linked-licences-view">
                <p><strong>Licence Number:</strong> {editableLicenceNumber || 'Not specified'}</p>
                {/* Future implementation: List of linked licences from data */}
            </div>
        );
    }
);
