import {useState, useImperativeHandle, forwardRef, useEffect, useRef} from 'react';
import {
    type Licence,
    Aggregate,
    LicenceSectionVerification,
    PrimaryType,
    NullableOfSubType,
    ContainedInInformation,
    InformationSource,
    Point,
    Purpose,
    AbstractionLimit
} from "../../../api/generated/apiClient.ts";
import {waleApiClient} from "../../../api/apiClient.ts";
import {type ILicenceSectionBody, type LicenceSectionBodyProps, type VerificationRequestPayload} from "../LicenceSection";
import {AggregateItem} from "./AggregateItem";
import {LicenceSectionVerificationInfo} from "../LicenceSectionVerificationInfo";
import {getVerificationTypeBackgroundColor} from "../../../utils/verificationUtils.ts";
import {compareAlphanumeric} from "../../../utils/formatting.ts";

const NO_AGGREGATES_ITEM_ID = 'None';

interface AggregatesProps extends LicenceSectionBodyProps {
    licence?: Licence;
    currentLicence?: Licence | null;
    onJumpToPage?: (pageNumber: number) => void;
    scrapedView?: boolean;
    history?: LicenceSectionVerification[];
}

export const Aggregates = forwardRef<ILicenceSectionBody, AggregatesProps>(
    ({licence, currentLicence, onJumpToPage, onItemVerificationRequested, onOpenReport, scrapedView, history}, ref) => {
        const [aggregates, setAggregates] = useState<Aggregate[]>([]);
        const [scrapedData, setScrapedData] = useState<Aggregate[] | null>(null);
        const [snapshotData, setSnapshotData] = useState<Aggregate[] | null>(null);

        // Tracks requests to calculate aggregate IDs
        const [aggregateIds, setAggregateIds] = useState<string[]>([]);
        const [scrapedDataIds, setScrapedDataIds] = useState<string[]>([]);
        const [snapshotDataIds, setSnapshotDataIds] = useState<string[]>([]);
        const aggregateIdsRequestRef = useRef(0);

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
            getData: async (itemId?: string) => {
                if (itemId) {
                    const idx = aggregateIds.indexOf(itemId);
                    return idx >= 0 ? aggregates[idx] : undefined;
                }
                return aggregates;
            },
            getScrapedData: async (itemId?: string) => {
                if (itemId) {
                    const idx = scrapedDataIds.indexOf(itemId);
                    return idx >= 0 ? scrapedData?.[idx] : undefined;
                }
                return scrapedData;
            },
            getSnapshotData: async (itemId?: string) => {
                if (itemId) {
                    const idx = snapshotDataIds.indexOf(itemId);
                    return idx >= 0 ? snapshotData?.[idx] : undefined;
                }
                return snapshotData;
            },
            getVerificationRequests: async (verificationType: string): Promise<VerificationRequestPayload[] | null> => {
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

                const [newId, oldId] = await waleApiClient.aggregateIds([currentItem, originalItem]);

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
        }), [aggregates, scrapedData, snapshotData, aggregateIds, scrapedDataIds, snapshotDataIds, editingIndex, originalItem]);

        useEffect(() => {
            const fetchAggregates = async () => {
                if (!licence?.dmsFileId) return;

                setIsLoading(true);
                setError(null);
                try {
                    const scraped = licence.abstractionLimits?.aggregates ?? [];
                    setScrapedData(scraped.map(a => Aggregate.fromJS(a)));
                    setScrapedDataIds(scraped.length > 0 ? await waleApiClient.aggregateIds(scraped) : []);

                    if (scrapedView) {
                        setAggregates(scraped);
                    } else if (currentLicence) {
                        const currentAggregates = currentLicence.abstractionLimits?.aggregates ?? [];
                        setAggregates(currentAggregates);
                        setSnapshotData(currentAggregates.map(a => Aggregate.fromJS(a)));
                        setSnapshotDataIds(currentAggregates.length > 0 ? await waleApiClient.aggregateIds(currentAggregates) : []);
                    }
                } catch (err) {
                    console.error("Error fetching aggregates:", err);
                    setError("Failed to load aggregates.");
                } finally {
                    setIsLoading(false);
                }
            };

            fetchAggregates();
        }, [licence?.dmsFileId, currentLicence, scrapedView]);

        // Debounced so editing PrimaryType/SubType/LinkedLicences doesn't fire a request per keystroke;
        // the request-sequence ref discards a stale response if a newer edit resolves first.
        useEffect(() => {
            if (aggregates.length === 0) {
                setAggregateIds([]);
                return;
            }

            const handle = setTimeout(async () => {
                const requestId = ++aggregateIdsRequestRef.current;
                try {
                    const ids = await waleApiClient.aggregateIds(aggregates);
                    if (requestId === aggregateIdsRequestRef.current) {
                        setAggregateIds(ids);
                    }
                } catch (err) {
                    console.error('Error computing aggregate ids:', err);
                }
            }, 350);

            return () => clearTimeout(handle);
        }, [aggregates]);

        const handleAddAggregate = () => {
            const newAggregate = new Aggregate({
                sourceLicenceNumber: licence?.licenceNumber?.value,
                sourceLicenceVersionId: licence?.licenceVersion?.licenceVersionId,
                primaryType: PrimaryType.NotSet,
                subType: NullableOfSubType.NotSet,
                linkedLicences: [''],
                containedIn: [new ContainedInInformation({source: InformationSource.Document, sectionName: '', linkReason: ''})],
                points: [new Point({id: '', description: ''})],
                purposes: [new Purpose({id: '', description: ''})],
                limits: [new AbstractionLimit({points: [], purposes: []})]
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
                                        backgroundColor: getVerificationTypeBackgroundColor('Confirmed'),
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
                    {!isLoading && !error && aggregates
                        .map((_, i) => i)
                        .sort((a, b) => compareAlphanumeric(aggregateIds[a], aggregateIds[b]))
                        .map((index) => {
                            const aggregate = aggregates[index];
                            const itemId = aggregateIds[index];
                            return (
                                <AggregateItem
                                    key={index}
                                    aggregate={aggregate}
                                    itemId={itemId}
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
                                    onOverride={async () => {
                                        if (editingIndex === index) {
                                            setIsWaitingForVerification(true);
                                            const [freshId] = await waleApiClient.aggregateIds([aggregates[index]]);
                                            onItemVerificationRequested?.(isAddingNew ? 'Added' : 'Edit', freshId);
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
                                backgroundColor: getVerificationTypeBackgroundColor('Added'),
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
