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
                <div className="linked-licences-edit" style={{ padding: '8px' }}>
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
                    <div style={{ marginTop: '16px', display: 'flex', justifyContent: 'center' }}>
                        <button 
                            onClick={handleAddLicence}
                            style={{ 
                                padding: '10px 24px', 
                                backgroundColor: '#1890ff', 
                                color: 'white', 
                                border: 'none', 
                                borderRadius: '4px', 
                                cursor: 'pointer', 
                                fontWeight: 'bold',
                                fontSize: '0.9rem',
                                boxShadow: '0 2px 0 rgba(0,0,0,0.045)'
                            }}
                        >
                            + Add Linked Licence
                        </button>
                    </div>
                </div>
            );
        }

        return (
            <div className="linked-licences-view" style={{ padding: '8px' }}>
                <div className="linked-licences-list">
                    {isLoading && <p style={{ textAlign: 'center', padding: '20px', color: '#888' }}>Loading linked licences...</p>}
                    {error && <p style={{ color: 'red', textAlign: 'center', padding: '20px' }}>{error}</p>}
                    {!isLoading && !error && linkedLicences.length === 0 && <p style={{ textAlign: 'center', padding: '20px', color: '#888' }}>No linked licences found.</p>}
                    {!isLoading && !error && linkedLicences.map((ll, index) => (
                        <LinkedLicenceItem key={index} linkedLicence={ll} onJumpToPage={onJumpToPage} />
                    ))}
                </div>
            </div>
        );
    }
);
