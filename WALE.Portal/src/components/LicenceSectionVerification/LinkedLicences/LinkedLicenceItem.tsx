import {
    LinkedLicence,
    NullableOfInformationDirection,
    ContainedInInformation,
    InformationSource,
    OutputListDataItem,
    LicenceSectionVerification
} from "../../../api/generated/apiClient.ts";
import {LicenceSectionVerificationInfo} from "../LicenceSectionVerificationInfo.tsx";
import NaldStatusTag from "../../NaldStatusTag.tsx";
import {hasOnlyOneOutgoingSection, hasAnyOutgoingSections, getFileId} from "../../../utils/verificationUtils.ts";

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
    outputListDataItem?: OutputListDataItem;
    data?: OutputListDataItem[];
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
                                      data,
                                      onOpenReport,
                                      scrapedView,
                                      history
                                  }: LinkedLicenceItemProps) => {
    const linkedLicence = linkedLicenceProp;

    if (!linkedLicence) {
        return null;
    }

    const linkedFilename = getFileId(data || [], linkedLicence.licenceNumber);

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
        if (onUpdate) {
            const newSection = new ContainedInInformation({
                source: InformationSource.Document,
                direction: NullableOfInformationDirection.Outgoing,
                sectionName: '',
                linkReason: '',
                isBecauseOfAggregate: false
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
        if (onOverride) {
            onOverride();
        }
    };

    const handleDiscardClick = (e: React.MouseEvent) => {
        e.preventDefault();
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
                                border: '1px solid #d9d9d9',
                                borderRadius: '4px',
                                boxSizing: 'border-box',
                                backgroundColor: !isAddingNew ? '#f0f0f0' : 'white'
                            }}
                        />
                        <NaldStatusTag status={linkedLicence.naldStatus}/>
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
                                border: '1px solid #d9d9d9',
                                borderRadius: '4px',
                                boxSizing: 'border-box',
                                backgroundColor: !isAddingNew ? '#f0f0f0' : 'white'
                            }}
                        />
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
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {(linkedLicence.containedIn || []).map((section, idx) => {
                            if (section.direction !== NullableOfInformationDirection.Outgoing) {
                                return null;
                            }
                            return (
                                <li key={idx} style={{
                                    marginBottom: '12px',
                                    padding: '12px',
                                    border: '1px solid #eee',
                                    borderRadius: '4px',
                                    backgroundColor: 'white'
                                }}>
                                    <div style={{
                                        display: 'grid',
                                        gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
                                        gap: '12px',
                                        alignItems: 'end'
                                    }}>
                                        <div>
                                            <label style={{
                                                display: 'block',
                                                fontSize: '0.75rem',
                                                marginBottom: '4px',
                                                fontWeight: '600'
                                            }}>Source:</label>
                                            <input
                                                type="text"
                                                value={section.source || ''}
                                                readOnly
                                                style={{
                                                    width: '100%',
                                                    padding: '4px 8px',
                                                    border: '1px solid #d9d9d9',
                                                    borderRadius: '4px',
                                                    boxSizing: 'border-box',
                                                    backgroundColor: '#f0f0f0'
                                                }}
                                            />
                                        </div>
                                        <div>
                                            <label style={{
                                                display: 'block',
                                                fontSize: '0.75rem',
                                                marginBottom: '4px',
                                                fontWeight: '600'
                                            }}>Section Name:</label>
                                            <input
                                                type="text"
                                                value={section.sectionName || ''}
                                                onChange={(e) => handleSectionChange(idx, 'sectionName', e.target.value)}
                                                style={{
                                                    width: '100%',
                                                    padding: '4px 8px',
                                                    border: '1px solid #d9d9d9',
                                                    borderRadius: '4px',
                                                    boxSizing: 'border-box'
                                                }}
                                            />
                                        </div>
                                        <div>
                                            <label style={{
                                                display: 'block',
                                                fontSize: '0.75rem',
                                                marginBottom: '4px',
                                                fontWeight: '600'
                                            }}>Link Reason:</label>
                                            <input
                                                type="text"
                                                value={section.linkReason || ''}
                                                onChange={(e) => handleSectionChange(idx, 'linkReason', e.target.value)}
                                                style={{
                                                    width: '100%',
                                                    padding: '4px 8px',
                                                    border: '1px solid #d9d9d9',
                                                    borderRadius: '4px',
                                                    boxSizing: 'border-box'
                                                }}
                                            />
                                        </div>
                                        <div style={{paddingBottom: '6px'}}>
                                            <label style={{
                                                fontSize: '0.75rem',
                                                display: 'flex',
                                                alignItems: 'center',
                                                cursor: 'pointer'
                                            }}>
                                                <input
                                                    type="checkbox"
                                                    checked={!!section.isBecauseOfAggregate}
                                                    onChange={(e) => handleSectionChange(idx, 'isBecauseOfAggregate', e.target.checked)}
                                                    style={{marginRight: '6px'}}
                                                />
                                                Because of Aggregate
                                            </label>
                                        </div>
                                        {section.pageNumber !== undefined && section.pageNumber !== null && section.pageNumber > 0 && (
                                            <div style={{paddingBottom: '2px'}}>
                                                <button
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        onJumpToPage && onJumpToPage(section.pageNumber!);
                                                    }}
                                                    title={`Jump to page ${section.pageNumber}`}
                                                    style={{
                                                        background: '#f0f0f0',
                                                        border: '1px solid #d9d9d9',
                                                        borderRadius: '4px',
                                                        cursor: 'pointer',
                                                        fontSize: '1rem',
                                                        padding: '2px 6px',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '4px'
                                                    }}
                                                >
                                                    📄 <span
                                                    style={{fontSize: '0.7rem'}}>Page {section.pageNumber}</span>
                                                </button>
                                            </div>
                                        )}
                                        <div style={{display: 'flex', justifyContent: 'flex-end', alignItems: 'end'}}>
                                            {!scrapedView && (
                                                <button
                                                    onClick={() => handleRemoveSection(idx)}
                                                    disabled={hasOnlyOneOutgoingSection(linkedLicence.containedIn)}
                                                    style={{
                                                        padding: '4px 8px',
                                                        fontSize: '0.75rem',
                                                        backgroundColor: hasOnlyOneOutgoingSection(linkedLicence.containedIn) ? '#f5f5f5' : '#ff7875',
                                                        color: hasOnlyOneOutgoingSection(linkedLicence.containedIn) ? 'rgba(0, 0, 0, 0.25)' : 'white',
                                                        border: hasOnlyOneOutgoingSection(linkedLicence.containedIn) ? '1px solid #d9d9d9' : 'none',
                                                        borderRadius: '4px',
                                                        cursor: hasOnlyOneOutgoingSection(linkedLicence.containedIn) ? 'not-allowed' : 'pointer'
                                                    }}
                                                >
                                                    Remove Section
                                                </button>
                                            )}
                                        </div>
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
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

    return (
        <div className="linked-licence-item" style={{padding: '12px', borderBottom: '1px solid #eee'}}>
            <div style={{display: 'flex', gap: '24px', flexWrap: 'wrap', marginBottom: '8px'}}>
                <p style={{margin: 0}}><strong>Linked Licence
                    Number:</strong> {linkedFilename ? (
                        <a href="#" onClick={(e) => {
                            e.preventDefault();
                            onOpenReport?.(linkedFilename);
                        }}>{linkedLicence.licenceNumber || 'N/A'}</a>
                    ) : (
                        linkedLicence.licenceNumber || 'N/A'
                    )}<NaldStatusTag
                    status={linkedLicence.naldStatus}/></p>
                <p style={{margin: 0}}><strong>Permit Number:</strong> {linkedLicence.permitNumber || 'N/A'}</p>
            </div>
            {hasAnyOutgoingSections(linkedLicence.containedIn) && (
                <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
                    <strong style={{display: 'block', marginBottom: '8px'}}>Contained In:</strong>
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {linkedLicence.containedIn!.map((section, idx) => {
                            if (section.direction !== NullableOfInformationDirection.Outgoing) {
                                return null;
                            }
                            return (
                                <li key={idx} style={{
                                    marginBottom: '8px',
                                    padding: '8px',
                                    backgroundColor: '#f9f9f9',
                                    borderRadius: '4px'
                                }}>
                                    <div style={{
                                        display: 'flex',
                                        flexWrap: 'wrap',
                                        gap: '8px 16px',
                                        alignItems: 'center'
                                    }}>
                                        <div><strong>Source:</strong> {section.source || 'N/A'}</div>
                                        <div><strong>Section:</strong> {section.sectionName || 'N/A'}</div>
                                        <div><strong>Link Reason:</strong> {section.linkReason || 'N/A'}</div>
                                        <div><strong>Because of
                                            Aggregate:</strong> {section.isBecauseOfAggregate ? 'Yes' : 'No'}</div>
                                        {section.pageNumber !== undefined && section.pageNumber !== null && section.pageNumber > 0 && (
                                            <button
                                                onClick={() => {
                                                    onJumpToPage && onJumpToPage(section.pageNumber!);
                                                }}
                                                title={`Jump to page ${section.pageNumber}`}
                                                style={{
                                                    background: 'none',
                                                    border: '1px solid #d9d9d9',
                                                    borderRadius: '4px',
                                                    cursor: 'pointer',
                                                    fontSize: '0.85rem',
                                                    padding: '2px 6px',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '4px'
                                                }}
                                            >
                                                📄 <span style={{fontSize: '0.75rem'}}>Page {section.pageNumber}</span>
                                            </button>
                                        )}
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
                </div>
            )}
            {!scrapedView && (onVerify || onReject || onOverride) && (
                <div style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'flex-start',
                    marginTop: '16px',
                    gap: '16px'
                }}>
                    <div style={{ flex: 1 }}>
                        {(() => {
                            const licenceNumber = linkedLicence.licenceNumber;
                            if (!licenceNumber || isEditing) return null;

                            const latestVerification = (history || [])
                                .filter(v => v.licenceSectionName === 'Linked Licences' && v.licenceSectionItemId === licenceNumber)
                                .sort((a, b) => {
                                    const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
                                    const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
                                    return dateB - dateA;
                                })[0];

                            if (!latestVerification) return null;

                            return <LicenceSectionVerificationInfo verification={latestVerification}/>;
                        })()}
                    </div>
                    <div style={{
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '8px',
                        alignItems: 'flex-end'
                    }}>
                        <div style={{
                            display: 'flex',
                            gap: '8px',
                            alignItems: 'center'
                        }}>
                            <button
                                onClick={onVerify}
                                style={{
                                    padding: '4px 12px',
                                    backgroundColor: '#52c41a',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '0.85rem'
                                }}
                            >
                                Confirm
                            </button>
                            <button
                                onClick={onReject}
                                style={{
                                    padding: '4px 12px',
                                    backgroundColor: '#ff4d4f',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '0.85rem'
                                }}
                            >
                                Remove
                            </button>
                            <button
                                onClick={onOverride}
                                style={{
                                    padding: '4px 12px',
                                    backgroundColor: '#1890ff',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '0.85rem'
                                }}
                            >
                                Edit
                            </button>
                        </div>
                        <div style={{
                            display: 'flex',
                            gap: '8px',
                            alignItems: 'center'
                        }}>
                            <button
                                onClick={onRequestBusinessReview}
                                style={{
                                    padding: '4px 12px',
                                    backgroundColor: 'darkorange',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '0.85rem'
                                }}
                            >
                                Request Business Review
                            </button>
                            <button
                                onClick={onCompleteBusinessReview}
                                style={{
                                    padding: '4px 12px',
                                    backgroundColor: 'purple',
                                    color: 'white',
                                    border: 'none',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '0.85rem'
                                }}
                            >
                                Complete Business Review
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
