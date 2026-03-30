import { useState, useImperativeHandle, forwardRef, useEffect } from 'react';
import { type Licence, type LinkedLicence, LinkedLicenceDirection } from "../../api/generated/apiClient.ts";
import { waleApiClient } from "../../api/apiClient.ts";
import { type ILicenceSectionBody, type LicenceSectionBodyProps } from "./LicenceSection";

interface LinkedLicencesProps extends LicenceSectionBodyProps {
    licence: Licence;
}

const LinkedLicenceItem = ({ linkedLicence }: { linkedLicence: LinkedLicence }) => {
    return (
        <div className="linked-licence-item" style={{ padding: '8px', borderBottom: '1px solid #eee' }}>
            <p style={{ margin: '0 0 4px 0' }}><strong>Linked Licence Number:</strong> {linkedLicence.licenceNumber || 'N/A'}</p>
            <p style={{ margin: '0 0 4px 0' }}><strong>Permit Number:</strong> {linkedLicence.permitNumber || 'N/A'}</p>
            {linkedLicence.containedIn && linkedLicence.containedIn.filter(s => s.direction === LinkedLicenceDirection.Outgoing).length > 0 && (
                <div style={{ marginTop: '8px', fontSize: '0.9rem' }}>
                    <strong>Contained In:</strong>
                    <ul style={{ margin: '4px 0 0 0', paddingLeft: '20px' }}>
                        {linkedLicence.containedIn
                            .filter(s => s.direction === LinkedLicenceDirection.Outgoing)
                            .map((section, idx) => (
                            <li key={idx} style={{ marginBottom: '4px' }}>
                                <div><strong>Section:</strong> {section.sectionName || 'N/A'}</div>
                                <div><strong>Link Reason:</strong> {section.linkReason || 'N/A'}</div>
                                <div><strong>Because of Aggregate:</strong> {section.isBecauseOfAggregate ? 'Yes' : 'No'}</div>
                                <div><strong>Line:</strong> {section.lineNumber ?? 'N/A'}, <strong>Page:</strong> {section.pageNumber ?? 'N/A'}</div>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
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
