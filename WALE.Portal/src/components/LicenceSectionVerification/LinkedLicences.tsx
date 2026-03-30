import { useState, useImperativeHandle, forwardRef, useEffect } from 'react';
import { type Licence, type LinkedLicence } from "../../api/generated/apiClient.ts";
import { waleApiClient } from "../../api/apiClient.ts";
import { type ILicenceSectionBody, type LicenceSectionBodyProps } from "./LicenceSection";

interface LinkedLicencesProps extends LicenceSectionBodyProps {
    licence: Licence;
}

const LinkedLicenceItem = ({ linkedLicence }: { linkedLicence: LinkedLicence }) => {
    return (
        <div className="linked-licence-item" style={{ padding: '8px', borderBottom: '1px solid #eee' }}>
            <p style={{ margin: '0 0 4px 0' }}><strong>Licence Number:</strong> {linkedLicence.licenceNumber || 'N/A'}</p>
            {linkedLicence.description && <p style={{ margin: '0', fontSize: '0.9rem', color: '#666' }}>{linkedLicence.description}</p>}
        </div>
    );
};

export const LinkedLicences = forwardRef<ILicenceSectionBody, LinkedLicencesProps>(
    ({ licence, isEditing }, ref) => {
        const [linkedLicences, setLinkedLicences] = useState<LinkedLicence[]>([]);
        const [isLoading, setIsLoading] = useState(false);
        const [error, setError] = useState<string | null>(null);

        // Expose data to parent via ref
        useImperativeHandle(ref, () => ({
            getData: () => ({
                // Add more fields here as needed
            })
        }));

        useEffect(() => {
            const fetchLinkedLicences = async () => {
                const permitNumber = licence.dmsPermitNumber;
                if (!permitNumber) return;

                setIsLoading(true);
                setError(null);
                try {
                    const results = await waleApiClient.getOutgoing(permitNumber);
                    setLinkedLicences(results || []);
                } catch (err) {
                    console.error("Error fetching linked licences:", err);
                    setError("Failed to load linked licences.");
                } finally {
                    setIsLoading(false);
                }
            };

            fetchLinkedLicences();
        }, [licence.dmsPermitNumber]);

        if (isEditing) {
            return (
                <div className="linked-licences-edit">
                    <p style={{ fontStyle: 'italic', fontSize: '0.9rem' }}>Editing Linked Licences...</p>
                </div>
            );
        }

        return (
            <div className="linked-licences-view">
                <div className="linked-licences-list">
                    <h4 style={{ margin: '0 0 10px 0', borderBottom: '2px solid #ddd', paddingBottom: '5px' }}>Linked Licences (Outgoing)</h4>
                    {isLoading && <p>Loading linked licences...</p>}
                    {error && <p style={{ color: 'red' }}>{error}</p>}
                    {!isLoading && !error && linkedLicences.length === 0 && <p>No linked licences found.</p>}
                    {!isLoading && !error && linkedLicences.map((ll, index) => (
                        <LinkedLicenceItem key={index} linkedLicence={ll} />
                    ))}
                </div>
            </div>
        );
    }
);
