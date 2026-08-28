import React from "react";
import {
    LinkedLicence,
    NullableOfInformationDirection,
    ContainedInInformation,
    InformationSource,
    LicenceSectionVerification
} from "../../../api/generated/apiClient.ts";
import {ValidationError} from "../ValidationError.tsx";
import {ContainedInList} from "../ContainedInList.tsx";
import {ContainedInEdit} from "../ContainedInEdit.tsx";
import {VerificationActions} from "../VerificationActions.tsx";
import {CollapsibleItem} from "../CollapsibleItem.tsx";
import NaldStatusTag from "../../NaldStatusTag.tsx";
import {hasOnlyOneOutgoingSection, hasAnyOutgoingSections} from "../../../utils/verificationUtils.ts";
import {useFileIdMap} from "../../../utils/useFileIdMap.tsx";
import NaldOnlyTag from "../../NaldOnlyTag.tsx";

interface LinkedLicenceItemProps {
    linkedLicence?: LinkedLicence;
    isEditing?: boolean;
    isAddingNew?: boolean;
    onUpdate?: (updated: LinkedLicence) => void;
    onRemove?: () => void;
    onDiscard?: () => void;
    onJumpToPage?: (pageNumber: number) => void;
    onVerify?: () => void;
    onReject?: () => void;
    onOverride?: () => void;
    onRequestBusinessReview?: () => void;
    onCompleteBusinessReview?: () => void;
    onOpenReport?: (fileId: string) => void;
    scrapedView?: boolean;
    history?: LicenceSectionVerification[];
}

export const LinkedLicenceItem = ({
                                      linkedLicence: linkedLicenceProp,
                                      isEditing,
                                      isAddingNew,
                                      onUpdate,
                                      onDiscard,
                                      onJumpToPage,
                                      onVerify,
                                      onReject,
                                      onOverride,
                                      onRequestBusinessReview,
                                      onCompleteBusinessReview,
                                      onOpenReport,
                                      scrapedView,
                                      history
                                  }: LinkedLicenceItemProps) => {
    const [errors, setErrors] = React.useState<Record<string, string>>({});
    const {getFileId} = useFileIdMap();
    const linkedLicence = linkedLicenceProp;

    if (!linkedLicence) {
        return null;
    }

    const linkedFilename = getFileId(linkedLicence.licenceNumber);

    const handleChange = (field: keyof LinkedLicence, value: any) => {
        if (onUpdate) {
            onUpdate(new LinkedLicence({...linkedLicence, [field]: value}));
        }
    };

    const handleSectionChange = (index: number, field: keyof ContainedInInformation, value: any) => {
        if (onUpdate && linkedLicence.containedIn) {
            const newSections = [...linkedLicence.containedIn];
            newSections[index] = new ContainedInInformation({...newSections[index], [field]: value});
            onUpdate(new LinkedLicence({...linkedLicence, containedIn: newSections}));
        }
    };

    const handleAddSection = () => {
        setErrors({});
        if (onUpdate) {
            const newSection = new ContainedInInformation({
                source: InformationSource.Document,
                direction: NullableOfInformationDirection.Outgoing,
                sectionName: '',
                linkReason: '',
            });
            const newSections = [...(linkedLicence.containedIn || []), newSection];
            onUpdate(new LinkedLicence({...linkedLicence, containedIn: newSections}));
        }
    };

    const handleRemoveSection = (index: number) => {
        if (onUpdate && linkedLicence.containedIn) {
            const newSections = linkedLicence.containedIn.filter((_, i) => i !== index);
            onUpdate(new LinkedLicence({...linkedLicence, containedIn: newSections}));
        }
    };

    const handleEdit = () => {
        const newErrors: Record<string, string> = {};

        if (isAddingNew) {
            if (!linkedLicence.licenceNumber || !linkedLicence.licenceNumber.trim()) {
                newErrors.licenceNumber = 'Licence Number is required';
            }
            if (!linkedLicence.permitNumber || !linkedLicence.permitNumber.trim()) {
                newErrors.permitNumber = 'Permit Number is required';
            }
        }

        (linkedLicence.containedIn || []).forEach((section, idx) => {
            if (section.direction === NullableOfInformationDirection.Outgoing) {
                if (!section.sectionName || !section.sectionName.trim()) {
                    newErrors[`section_${idx}_sectionName`] = 'Section Name is required';
                }
                if (!section.linkReason || !section.linkReason.trim()) {
                    newErrors[`section_${idx}_linkReason`] = 'Link Reason is required';
                }
            }
        });

        setErrors(newErrors);

        if (Object.keys(newErrors).length === 0 && onOverride) {
            onOverride();
        }
    };

    const handleDiscardClick = (e: React.MouseEvent) => {
        e.preventDefault();
        setErrors({});
        if (onDiscard) {
            onDiscard();
        }
    };

    if (isEditing) {
        return (
            <div className="linked-licence-item-edit" style={{
                padding: '16px',
                border: '1px solid #d9d9d9',
                borderRadius: '4px',
                marginBottom: '16px',
                backgroundColor: '#fafafa'
            }}>
                <div style={{display: 'flex', gap: '12px', marginBottom: '16px', alignItems: 'flex-start'}}>
                    <div style={{flex: 1}}>
                        <label style={{display: 'block', fontSize: '0.85rem', fontWeight: 'bold', marginBottom: '4px'}}>Linked
                            Licence Number:</label>
                        <input
                            type="text"
                            value={linkedLicence.licenceNumber || ''}
                            onChange={(e) => handleChange('licenceNumber', e.target.value)}
                            readOnly={!isAddingNew}
                            style={{
                                width: '80%',
                                padding: '6px 8px',
                                border: errors.licenceNumber ? '1px solid #ff4d4f' : '1px solid #d9d9d9',
                                borderRadius: '4px',
                                boxSizing: 'border-box',
                                backgroundColor: !isAddingNew ? '#f0f0f0' : 'white'
                            }}
                        />
                        <ValidationError message={errors.licenceNumber}/>
                        <NaldStatusTag status={linkedLicence.naldStatus}/>
                        <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                    </div>
                    <div style={{flex: 1}}>
                        <label style={{display: 'block', fontSize: '0.85rem', fontWeight: 'bold', marginBottom: '4px'}}>Permit
                            Number:</label>
                        <input
                            type="text"
                            value={linkedLicence.permitNumber || ''}
                            onChange={(e) => handleChange('permitNumber', e.target.value)}
                            readOnly={!isAddingNew}
                            style={{
                                width: '100%',
                                padding: '6px 8px',
                                border: errors.permitNumber ? '1px solid #ff4d4f' : '1px solid #d9d9d9',
                                borderRadius: '4px',
                                boxSizing: 'border-box',
                                backgroundColor: !isAddingNew ? '#f0f0f0' : 'white'
                            }}
                        />
                        <ValidationError message={errors.permitNumber}/>
                    </div>

                    <div style={{marginTop: '16px'}}>
                        <label style={{
                            fontSize: '0.85rem',
                            display: 'flex',
                            alignItems: 'center',
                            cursor: 'pointer'
                        }}>
                            <input
                                type="checkbox"
                                checked={!!linkedLicence.isBecauseOfAggregate}
                                onChange={(e) => handleChange('isBecauseOfAggregate', e.target.checked)}
                                style={{marginRight: '6px'}}
                            />
                            Because of Aggregate
                        </label>
                    </div>
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        marginBottom: '8px'
                    }}>
                        <strong style={{fontSize: '0.9rem'}}>Contained In (Sections):</strong>
                        {!scrapedView && (
                            <button
                                onClick={handleAddSection}
                                style={{
                                    padding: '4px 12px',
                                    fontSize: '0.8rem',
                                    backgroundColor: '#52c41a',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer'
                                }}
                            >
                                + Add Section
                            </button>
                        )}
                    </div>
                    {(() => {
                        const outgoingSections = (linkedLicence.containedIn || [])
                            .map((section, originalIndex) => ({section, originalIndex}))
                            .filter(({section}) => section.direction === NullableOfInformationDirection.Outgoing);

                        return (
                            <ContainedInEdit
                                sections={outgoingSections.map(o => o.section)}
                                onChange={(idx, field, value) => handleSectionChange(outgoingSections[idx].originalIndex, field, value)}
                                onRemove={(idx) => handleRemoveSection(outgoingSections[idx].originalIndex)}
                                onJumpToPage={onJumpToPage}
                                showLinkReason
                                canRemove={() => !hasOnlyOneOutgoingSection(linkedLicence.containedIn)}
                                getFieldError={(idx, field) => {
                                    const originalIndex = outgoingSections[idx].originalIndex;
                                    return field === 'sectionName'
                                        ? errors[`section_${originalIndex}_sectionName`]
                                        : errors[`section_${originalIndex}_linkReason`];
                                }}
                            />
                        );
                    })()}
                </div>
                <div style={{display: 'flex', gap: '8px', marginTop: '24px', justifyContent: 'flex-end'}}>
                    <button
                        onClick={handleEdit}
                        style={{
                            padding: '6px 20px',
                            backgroundColor: '#1890ff',
                            color: 'white',
                            border: 'none',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontWeight: '600'
                        }}
                    >
                        Save
                    </button>
                    <button
                        onClick={handleDiscardClick}
                        style={{
                            padding: '6px 20px',
                            backgroundColor: '#f0f0f0',
                            color: 'rgba(0, 0, 0, 0.85)',
                            border: '1px solid #d9d9d9',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            fontWeight: '600'
                        }}
                    >
                        Discard
                    </button>
                </div>
            </div>
        );
    }

    const summary = (
        <div style={{display: 'flex', gap: '16px', flexWrap: 'wrap', alignItems: 'center', fontSize: '0.9rem'}}>
            <strong>{linkedFilename ? (
                <a href="#" onClick={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    onOpenReport?.(linkedFilename);
                }}>{linkedLicence.licenceNumber || 'N/A'}</a>
            ) : (
                linkedLicence.licenceNumber || 'N/A'
            )}</strong>
            <NaldStatusTag status={linkedLicence.naldStatus}/>
            <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
        </div>
    );

    return (
        <CollapsibleItem summary={summary} defaultOpen={true}>
            <div style={{display: 'flex', gap: '24px', flexWrap: 'wrap', marginBottom: '8px'}}>
                <p style={{margin: 0}}><strong>Linked Licence Number:</strong>
                    {linkedFilename ? (
                        <a href="#" onClick={(e) => {
                            e.preventDefault();
                            onOpenReport?.(linkedFilename);
                        }}>{linkedLicence.licenceNumber || 'N/A'}</a>
                    ) : (
                        linkedLicence.licenceNumber || 'N/A'
                    )}
                    <NaldStatusTag status={linkedLicence.naldStatus}/>
                    <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                </p>
                <p style={{margin: 0}}><strong>Permit Number:</strong> {linkedLicence.permitNumber || 'N/A'}</p>
                <div style={{marginBottom: '8px'}}><strong>Because of
                    Aggregate:</strong> {linkedLicence.isBecauseOfAggregate ? 'Yes' : 'No'}</div>
            </div>
            {hasAnyOutgoingSections(linkedLicence.containedIn) && (
                <ContainedInList
                    sections={(linkedLicence.containedIn || []).filter(s => s.direction === NullableOfInformationDirection.Outgoing)}
                    onJumpToPage={onJumpToPage}
                    showLinkReason
                />
            )}
            <VerificationActions
                scrapedView={scrapedView}
                history={history}
                licenceSectionName="Linked Licences"
                itemId={linkedLicence.licenceNumber}
                onVerify={onVerify}
                onReject={onReject}
                onOverride={onOverride}
                onRequestBusinessReview={onRequestBusinessReview}
                onCompleteBusinessReview={onCompleteBusinessReview}
            />
        </CollapsibleItem>
    );
};
