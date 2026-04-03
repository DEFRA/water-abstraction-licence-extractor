import { useState, useImperativeHandle, forwardRef, useEffect } from 'react';
import { type Licence, LinkedLicence } from "../../api/generated/apiClient.ts";
import { waleApiClient } from "../../api/apiClient.ts";
import { type ILicenceSectionBody, type LicenceSectionBodyProps } from "./LicenceSection";
import { LinkedLicenceItem } from "./LinkedLicenceItem";

interface LinkedLicencesProps extends LicenceSectionBodyProps {
    licence: Licence;
    onJumpToPage?: (pageNumber: number) => void;
}

export const LinkedLicences = forwardRef<ILicenceSectionBody, LinkedLicencesProps>(
    ({ licence, isEditing, onJumpToPage }, ref) => {
        const [linkedLicences, setLinkedLicences] = useState<LinkedLicence[]>([]);
        const [isLoading, setIsLoading] = useState(false);
        const [error, setError] = useState<string | null>(null);

        // Expose data to parent via ref
        useImperativeHandle(ref, () => ({
            getData: () => ({
                linkedLicences: linkedLicences
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

        const handleAddLicence = () => {
            const newLicence = new LinkedLicence({
                licenceNumber: '',
                permitNumber: '',
                containedIn: []
            });
            setLinkedLicences([...linkedLicences, newLicence]);
        };

        const handleUpdateLicence = (index: number, updated: LinkedLicence) => {
            const newList = [...linkedLicences];
            newList[index] = updated;
            setLinkedLicences(newList);
        };

        const handleRemoveLicence = (index: number) => {
            const newList = linkedLicences.filter((_, i) => i !== index);
            setLinkedLicences(newList);
        };

        if (isEditing) {
            return (
                <div className="linked-licences-edit">
                    <div className="linked-licences-list">
                        {linkedLicences.map((ll, index) => (
                            <LinkedLicenceItem 
                                key={index} 
                                linkedLicence={ll} 
                                isEditing={true}
                                onUpdate={(updated) => handleUpdateLicence(index, updated)}
                                onRemove={() => handleRemoveLicence(index)}
                                onJumpToPage={onJumpToPage}
                            />
                        ))}
                    </div>
                    <div style={{ marginTop: '16px', padding: '8px', borderTop: '1px solid #eee' }}>
                        <button 
                            onClick={handleAddLicence}
                            style={{ width: '100%', padding: '8px', backgroundColor: '#1890ff', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 'bold' }}
                        >
                            + Add Linked Licence
                        </button>
                    </div>
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
                        <LinkedLicenceItem key={index} linkedLicence={ll} onJumpToPage={onJumpToPage} />
                    ))}
                </div>
            </div>
        );
    }
);
