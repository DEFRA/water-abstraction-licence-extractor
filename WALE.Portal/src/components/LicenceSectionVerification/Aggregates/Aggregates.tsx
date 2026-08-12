import {useState, useImperativeHandle, forwardRef, useEffect} from 'react';
import {
    type Licence,
    Aggregate,
    LicenceSectionVerification,
    PrimaryType,
    NullableOfSubType
} from "../../../api/generated/apiClient.ts";
import {waleApiClient} from "../../../api/apiClient.ts";
import {type ILicenceSectionBody, type LicenceSectionBodyProps, type VerificationRequestPayload} from "../LicenceSection";
import {AggregateItem} from "./AggregateItem";
import {LicenceSectionVerificationInfo} from "../LicenceSectionVerificationInfo";
import {computeAggregateId} from "../../../utils/aggregateUtils.ts";

const NO_AGGREGATES_ITEM_ID = 'None';

interface AggregatesProps extends LicenceSectionBodyProps {
    licence?: Licence;
    processRunId?: number;
    onJumpToPage?: (pageNumber: number) => void;
    scrapedView?: boolean;
    history?: LicenceSectionVerification[];
}

export const Aggregates = forwardRef<ILicenceSectionBody, AggregatesProps>(
    ({licence, processRunId, onJumpToPage, onItemVerificationRequested, onOpenReport, scrapedView, history}, ref) => {
        const [aggregates, setAggregates] = useState<Aggregate[]>([]);
        const [scrapedData, setScrapedData] = useState<Aggregate[] | null>(null);
        const [snapshotData, setSnapshotData] = useState<Aggregate[] | null>(null);

        const noAggregatesVerification = (history || [])
            .filter(v => v.licenceSectionName === 'Aggregates' && v.licenceSectionItemId === NO_AGGREGATES_ITEM_ID)
            .sort((a, b) => {
                const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
                const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
                return dateB - dateA;
            })[0];

        const [isLoading, setIsLoading] = useState(false);
        const [error, setError] = useState<string | null>(null);
        const [editingIndex, setEditingIndex] = useState<number | null>(null);
        const [originalItem, setOriginalItem] = useState<Aggregate | null>(null);
        const [isAddingNew, setIsAddingNew] = useState(false);
        const [isWaitingForVerification, setIsWaitingForVerification] = useState(false);

        // Expose data to parent via ref
        useImperativeHandle(ref, () => ({
            getData: (itemId?: string) => {
                if (itemId) {
                    return aggregates.find(a => computeAggregateId(a) === itemId);
                }
                return aggregates;
            },
            getScrapedData: (itemId?: string) => {
                if (itemId) {
                    return scrapedData?.find(a => computeAggregateId(a) === itemId);
                }
                return scrapedData;
            },
            getSnapshotData: (itemId?: string) => {
                if (itemId) {
                    return snapshotData?.find(a => computeAggregateId(a) === itemId);
                }
                return snapshotData;
            },
            getVerificationRequests: (verificationType: string): VerificationRequestPayload[] | null => {
                // Editing an aggregate's PrimaryType/SubType/LinkedLicences changes its computed Id — that
                // can't be expressed as a single 'Edited' verification (which assumes the item's identity
                // is unchanged), so it must be saved as Removed(oldId) + Added(newId) instead.
                if (verificationType !== 'Edited' || editingIndex === null || !originalItem) {
                    return null;
                }

                const currentItem = aggregates[editingIndex];
                if (!currentItem) {
                    return null;
                }

                const newId = computeAggregateId(currentItem);
                const oldId = computeAggregateId(originalItem);

                if (newId === oldId) {
                    return null; // no Id-affecting field changed — default single 'Edited' POST
                }

                return [
                    {verificationType: 'Removed', itemId: oldId, snapshotData: originalItem},
                    {verificationType: 'Added', itemId: newId, data: currentItem}
                ];
            },
            onVerificationCancelled: () => {
                setIsWaitingForVerification(false);
            }
        }), [aggregates, scrapedData, snapshotData, editingIndex, originalItem]);

        useEffect(() => {
            const fetchAggregates = async () => {
                if (!licence?.dmsFileId) return;

                setIsLoading(true);
                setError(null);
                try {
                    const scraped = licence.abstractionLimits?.aggregates ?? [];
                    setScrapedData(scraped.map(a => Aggregate.fromJS(a)));

                    if (scrapedView) {
                        setAggregates(scraped);
                    } else if (processRunId) {
                        const merged = await waleApiClient.licence(licence.dmsFileId, processRunId, true);
                        const currentAggregates = merged.abstractionLimits?.aggregates ?? [];
                        setAggregates(currentAggregates);
                        setSnapshotData(currentAggregates.map(a => Aggregate.fromJS(a)));
                    }
                } catch (err) {
                    console.error("Error fetching aggregates:", err);
                    setError("Failed to load aggregates.");
                } finally {
                    setIsLoading(false);
                }
            };

            fetchAggregates();
        }, [licence?.dmsFileId, processRunId, scrapedView]);

        const handleAddAggregate = () => {
            const newAggregate = new Aggregate({
                sourceLicenceNumber: licence?.licenceNumber?.value,
                sourceLicenceVersionId: licence?.licenceVersion?.licenceVersionId,
                primaryType: PrimaryType.NotSet,
                subType: NullableOfSubType.NotSet,
                linkedLicences: [],
                containedIn: [],
                points: [],
                purposes: [],
                limits: []
            });
            const newList = [...aggregates, newAggregate];
            setAggregates(newList);
            setEditingIndex(newList.length - 1);
            setIsAddingNew(true);
            setOriginalItem(null);
            setIsWaitingForVerification(false);
        };

        const handleUpdateAggregate = (index: number, updated: Aggregate) => {
            const newList = [...aggregates];
            newList[index] = updated;
            setAggregates(newList);
        };

        const handleRemoveAggregate = (index: number) => {
            const newList = aggregates.filter((_, i) => i !== index);
            setAggregates(newList);
            if (editingIndex === index) {
                setEditingIndex(null);
                setIsAddingNew(false);
                setOriginalItem(null);
            } else if (editingIndex !== null && editingIndex > index) setEditingIndex(editingIndex - 1);
        };

        const handleDiscard = () => {
            if (editingIndex === null) return;

            if (isAddingNew) {
                handleRemoveAggregate(editingIndex);
            } else if (originalItem) {
                const newList = [...aggregates];
                newList[editingIndex] = originalItem;
                setAggregates(newList);
                setEditingIndex(null);
                setOriginalItem(null);
            } else {
                setEditingIndex(null);
            }
            setIsAddingNew(false);
        };

        return (
            <div className="aggregates-container" style={{padding: '8px'}}>
                <div className="aggregates-list">
                    {isLoading &&
                        <p style={{textAlign: 'center', padding: '20px', color: '#888'}}>Loading aggregates...</p>}
                    {error && <p style={{color: 'red', textAlign: 'center', padding: '20px'}}>{error}</p>}
                    {!isLoading && !error && aggregates.length === 0 && (
                        <div style={{textAlign: 'center', padding: '20px'}}>
                            <p style={{color: '#888', marginBottom: '16px'}}>No aggregates found.</p>
                            {noAggregatesVerification && (
                                <LicenceSectionVerificationInfo verification={noAggregatesVerification}/>
                            )}
                            {!scrapedView && (
                                <button
                                    onClick={() => onItemVerificationRequested?.('ConfirmNone', NO_AGGREGATES_ITEM_ID)}
                                    style={{
                                        padding: '6px 20px',
                                        backgroundColor: '#52c41a',
                                        color: 'white',
                                        border: 'none',
                                        borderRadius: '4px',
                                        cursor: 'pointer',
                                        fontWeight: '600',
                                        fontSize: '0.85rem',
                                        marginTop: noAggregatesVerification ? '12px' : '0'
                                    }}
                                >
                                    Confirm No Aggregates
                                </button>
                            )}
                        </div>
                    )}
                    {!isLoading && !error && aggregates.length > 0 && noAggregatesVerification && (
                        <div style={{
                            marginBottom: '12px',
                            padding: '8px',
                            backgroundColor: '#f9f9f9',
                            borderRadius: '4px'
                        }}>
                            <LicenceSectionVerificationInfo verification={noAggregatesVerification}/>
                        </div>
                    )}
                    {!isLoading && !error && aggregates.map((aggregate, index) => {
                        const itemId = computeAggregateId(aggregate);
                        return (
                            <AggregateItem
                                key={index}
                                aggregate={aggregate}
                                isEditing={editingIndex === index && !isWaitingForVerification}
                                isAddingNew={isAddingNew && editingIndex === index && !isWaitingForVerification}
                                onUpdate={(updated) => handleUpdateAggregate(index, updated)}
                                onRemove={() => handleRemoveAggregate(index)}
                                onDiscard={handleDiscard}
                                onJumpToPage={onJumpToPage}
                                onVerify={() => onItemVerificationRequested?.('Confirm', itemId)}
                                onReject={() => onItemVerificationRequested?.('Remove', itemId)}
                                onRequestBusinessReview={() => onItemVerificationRequested?.('RequestBusinessReview', itemId)}
                                onCompleteBusinessReview={() => onItemVerificationRequested?.('CompleteBusinessReview', itemId)}
                                onOverride={() => {
                                    if (editingIndex === index) {
                                        setIsWaitingForVerification(true);
                                        onItemVerificationRequested?.(isAddingNew ? 'Added' : 'Edit', itemId);
                                    } else {
                                        setEditingIndex(index);
                                        setIsAddingNew(false);
                                        setOriginalItem(Aggregate.fromJS(aggregate));
                                        setIsWaitingForVerification(false);
                                    }
                                }}
                                onOpenReport={onOpenReport}
                                scrapedView={scrapedView}
                                history={history}
                            />
                        );
                    })}
                </div>
                <div style={{marginTop: '16px', display: 'flex', justifyContent: 'center'}}>
                    {!scrapedView && (
                        <button
                            onClick={handleAddAggregate}
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
                            + Add Aggregate
                        </button>
                    )}
                </div>
            </div>
        );
    }
);
