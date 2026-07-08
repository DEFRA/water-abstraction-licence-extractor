import {useState, useImperativeHandle, forwardRef, useEffect} from 'react';
import {type Licence, LinkedLicence} from "../../api/generated/apiClient.ts";
import {waleApiClient} from "../../api/apiClient.ts";
import {type ILicenceSectionBody, type LicenceSectionBodyProps} from "./LicenceSection";
import {LinkedLicenceItem} from "./LinkedLicenceItem";
import {LicenceSectionVerificationInfo} from "./LicenceSectionVerificationInfo";

interface LinkedLicencesProps extends LicenceSectionBodyProps {
    licence?: Licence;
    onJumpToPage?: (pageNumber: number) => void;
    scrapedView?: boolean;
}

export const LinkedLicences = forwardRef<ILicenceSectionBody, LinkedLicencesProps>(
    ({licence, onJumpToPage, onItemVerificationRequested, outputListDataItem, scrapedView}, ref) => {
        const [linkedLicences, setLinkedLicences] = useState<LinkedLicence[]>([]);
        const [scrapedData, setScrapedData] = useState<LinkedLicence[] | null>(null);
        const [snapshotData, setSnapshotData] = useState<LinkedLicence[] | null>(null);

        const noneOutgoingVerification = outputListDataItem?.latestLicenceSectionVerifications?.find(
            v => v.licenceSectionItemId === 'None Outgoing'
        );

        const [isLoading, setIsLoading] = useState(false);
        const [error, setError] = useState<string | null>(null);
        const [editingIndex, setEditingIndex] = useState<number | null>(null);
        const [originalItem, setOriginalItem] = useState<LinkedLicence | null>(null);
        const [isAddingNew, setIsAddingNew] = useState(false);
        const [isWaitingForVerification, setIsWaitingForVerification] = useState(false);

        // Expose data to parent via ref
        useImperativeHandle(ref, () => ({
            getData: (itemId?: string) => {
                if (itemId) {
                    return linkedLicences.find((ll, index) => (ll.licenceNumber || ll.permitNumber || `item-${index}`) === itemId);
                }
                return linkedLicences;
            },
            getScrapedData: (itemId?: string) => {
                if (itemId) {
                    return scrapedData?.find((ll, index) => (ll.licenceNumber || ll.permitNumber || `item-${index}`) === itemId);
                }
                return scrapedData;
            },
            getSnapshotData: (itemId?: string) => {
                if (itemId) {
                    return snapshotData?.find((ll, index) => (ll.licenceNumber || ll.permitNumber || `item-${index}`) === itemId);
                }
                return snapshotData;
            },
            onVerificationCancelled: () => {
                setIsWaitingForVerification(false);
            }
        }), [linkedLicences, scrapedData, snapshotData]);

        useEffect(() => {
            const fetchLinkedLicences = async () => {
                const permitNumber = licence?.dmsPermitNumber;
                if (!permitNumber) return;

                setIsLoading(true);
                setError(null);
                try {
                    const results = await waleApiClient.getOutgoing(permitNumber);
                    setLinkedLicences(results || []);
                    setScrapedData(results?.map(ll => LinkedLicence.fromJS(ll)) || []);

                    if (!scrapedView) {
                        const verifications = [...(outputListDataItem?.latestLicenceSectionVerifications || [])]
                            .sort((a, b) => (b.createdDateTimeUtc?.getTime() || 0) - (a.createdDateTimeUtc?.getTime() || 0));

                        const outgoingLinkedLicences = [...(results || [])];

                        verifications.forEach(verification => {
                            if (verification.licenceSectionItemId === 'None Outgoing') {
                                return;
                            }

                            try {
                                const rawValue = verification.licenceSectionOverrideValue ?? verification.licenceSectionScrapedValue;
                                if (!rawValue) return;

                                const overrideLicence = LinkedLicence.fromJS(JSON.parse(rawValue));
                                const existingIndex = outgoingLinkedLicences.findIndex(x => x.licenceNumber === verification.licenceSectionItemId);

                                switch (verification.verificationType) {
                                    case "Confirmed":
                                    case "AutoConfirm":
                                        if (existingIndex === -1) {
                                            outgoingLinkedLicences.push(overrideLicence);
                                        } else if (verification.scrapedDataIsDifferent) {
                                            outgoingLinkedLicences.splice(existingIndex, 1, overrideLicence);
                                        }
                                        break;
                                    case "Removed":
                                        if (existingIndex !== -1) {
                                            outgoingLinkedLicences.splice(existingIndex, 1);
                                        }
                                        break;
                                    case "Edited":
                                    case "Added":
                                        if (existingIndex !== -1) {
                                            outgoingLinkedLicences.splice(existingIndex, 1);
                                        }
                                        outgoingLinkedLicences.push(overrideLicence);
                                        break;
                                }
                            } catch (e) {
                                console.error("Failed to process verification", verification, e);
                            }
                        });

                        setLinkedLicences(outgoingLinkedLicences);
                        setSnapshotData(outgoingLinkedLicences?.map(ll => LinkedLicence.fromJS(ll)) || []);
                    }
                } catch (err) {
                    console.error("Error fetching linked licences:", err);
                    setError("Failed to load linked licences.");
                } finally {
                    setIsLoading(false);
                }
            };

            fetchLinkedLicences();
        }, [licence?.dmsPermitNumber]);

        const handleAddLicence = () => {
            const newLicence = new LinkedLicence({
                licenceNumber: '',
                permitNumber: '',
                containedIn: []
            });
            const newList = [...linkedLicences, newLicence];
            setLinkedLicences(newList);
            setEditingIndex(newList.length - 1);
            setIsAddingNew(true);
            setOriginalItem(null);
            setIsWaitingForVerification(false);
        };

        const handleUpdateLicence = (index: number, updated: LinkedLicence) => {
            const newList = [...linkedLicences];
            newList[index] = updated;
            setLinkedLicences(newList);
        };

        const handleRemoveLicence = (index: number) => {
            const newList = linkedLicences.filter((_, i) => i !== index);
            setLinkedLicences(newList);
            if (editingIndex === index) {
                setEditingIndex(null);
                setIsAddingNew(false);
                setOriginalItem(null);
            } else if (editingIndex !== null && editingIndex > index) setEditingIndex(editingIndex - 1);
        };

        const handleDiscard = () => {
            if (editingIndex === null) return;

            if (isAddingNew) {
                handleRemoveLicence(editingIndex);
            } else if (originalItem) {
                const newList = [...linkedLicences];
                newList[editingIndex] = originalItem;
                setLinkedLicences(newList);
                setEditingIndex(null);
                setOriginalItem(null);
            } else {
                setEditingIndex(null);
            }
            setIsAddingNew(false);
        };

        return (
            <div className="linked-licences-container" style={{padding: '8px'}}>
                <div className="linked-licences-list">
                    {isLoading &&
                        <p style={{textAlign: 'center', padding: '20px', color: '#888'}}>Loading linked licences...</p>}
                    {error && <p style={{color: 'red', textAlign: 'center', padding: '20px'}}>{error}</p>}
                    {!isLoading && !error && linkedLicences.length === 0 && (
                        <div style={{textAlign: 'center', padding: '20px'}}>
                            <p style={{color: '#888', marginBottom: '16px'}}>No outgoing linked licences found.</p>
                            {noneOutgoingVerification && (
                                <LicenceSectionVerificationInfo verification={noneOutgoingVerification}/>
                            )}
                            {!scrapedView && (
                                <button
                                    onClick={() => onItemVerificationRequested?.('ConfirmNone', 'None Outgoing')}
                                    style={{
                                        padding: '6px 20px',
                                        backgroundColor: '#52c41a',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '4px',
                                        cursor: 'pointer',
                                        fontWeight: '600',
                                        fontSize: '0.85rem',
                                        marginTop: noneOutgoingVerification ? '12px' : '0'
                                    }}
                                >
                                    Confirm No Outgoing Linked Licences
                                </button>
                            )}
                        </div>
                    )}
                    {!isLoading && !error && linkedLicences.length > 0 && noneOutgoingVerification && (
                        <div style={{
                            marginBottom: '12px',
                            padding: '8px',
                            backgroundColor: '#f9f9f9',
                            borderRadius: '4px'
                        }}>
                            <LicenceSectionVerificationInfo verification={noneOutgoingVerification}/>
                        </div>
                    )}
                    {!isLoading && !error && linkedLicences.map((ll, index) => (
                        <LinkedLicenceItem
                            key={index}
                            linkedLicence={ll}
                            isEditing={editingIndex === index && !isWaitingForVerification}
                            isAddingNew={isAddingNew && editingIndex === index && !isWaitingForVerification}
                            onUpdate={(updated) => handleUpdateLicence(index, updated)}
                            onRemove={() => handleRemoveLicence(index)}
                            onDiscard={handleDiscard}
                            onJumpToPage={onJumpToPage}
                            onVerify={() => onItemVerificationRequested?.('Confirm', (ll.licenceNumber || ll.permitNumber || `item-${index}`))}
                            onReject={() => onItemVerificationRequested?.('Remove', (ll.licenceNumber || ll.permitNumber || `item-${index}`))}
                            onOverride={() => {
                                if (editingIndex === index) {
                                    setIsWaitingForVerification(true);
                                    onItemVerificationRequested?.(isAddingNew ? 'Added' : 'Edit', (ll.licenceNumber || ll.permitNumber || `item-${index}`));
                                } else {
                                    setEditingIndex(index);
                                    setIsAddingNew(false);
                                    setOriginalItem(LinkedLicence.fromJS(ll));
                                    setIsWaitingForVerification(false);
                                }
                            }}
                            outputListDataItem={outputListDataItem}
                            scrapedView={scrapedView}
                        />
                    ))}
                </div>
                <div style={{marginTop: '16px', display: 'flex', justifyContent: 'center'}}>
                    {!scrapedView && (
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
                    )}
                </div>
            </div>
        );
    }
);
