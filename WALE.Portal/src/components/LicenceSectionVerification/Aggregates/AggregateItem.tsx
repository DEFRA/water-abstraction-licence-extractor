import React from "react";
import {
    Aggregate,
    ContainedInInformation,
    AbstractionLimit,
    Point,
    Purpose,
    TimePeriod,
    TimeCutoff,
    PrimaryType,
    NullableOfSubType,
    LimitPeriodType,
    NullableOfCutoffType,
    InformationSource,
    LicenceSectionVerification
} from "../../../api/generated/apiClient.ts";
import {LicenceSectionVerificationInfo} from "../LicenceSectionVerificationInfo.tsx";
import {ValidationError} from "../ValidationError.tsx";
import {computeAggregateId} from "../../../utils/aggregateUtils.ts";

interface AggregateItemProps {
    aggregate?: Aggregate;
    isEditing?: boolean;
    isAddingNew?: boolean;
    onUpdate?: (updated: Aggregate) => void;
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

const labelStyle: React.CSSProperties = {display: 'block', fontSize: '0.75rem', marginBottom: '4px', fontWeight: 600};
const inputStyle: React.CSSProperties = {width: '100%', padding: '4px 8px', border: '1px solid #d9d9d9', borderRadius: '4px', boxSizing: 'border-box'};
const rowStyle: React.CSSProperties = {display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: '12px', alignItems: 'end'};
const cardStyle: React.CSSProperties = {marginBottom: '10px', padding: '10px', border: '1px solid #eee', borderRadius: '4px', backgroundColor: 'white'};
const addButtonStyle: React.CSSProperties = {padding: '4px 12px', fontSize: '0.8rem', backgroundColor: '#52c41a', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer'};
const removeButtonStyle: React.CSSProperties = {padding: '4px 8px', fontSize: '0.75rem', backgroundColor: '#ff7875', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer'};

export const AggregateItem = ({
                                   aggregate: aggregateProp,
                                   isEditing,
                                   onUpdate,
                                   onDiscard,
                                   onJumpToPage,
                                   onVerify,
                                   onReject,
                                   onOverride,
                                   onRequestBusinessReview,
                                   onCompleteBusinessReview,
                                   scrapedView,
                                   history
                               }: AggregateItemProps) => {
    const [errors, setErrors] = React.useState<Record<string, string>>({});
    const aggregate = aggregateProp;

    if (!aggregate) {
        return null;
    }

    const limits = (aggregate.limits ?? []) as AbstractionLimit[];
    const containedIn = (aggregate.containedIn ?? []) as ContainedInInformation[];
    const points = (aggregate.points ?? []) as Point[];
    const purposes = (aggregate.purposes ?? []) as Purpose[];
    const linkedLicences = aggregate.linkedLicences ?? [];

    const update = (changes: Partial<Aggregate>) => {
        if (onUpdate) {
            onUpdate(new Aggregate({...aggregate, ...changes}));
        }
    };

    const handleChange = (field: keyof Aggregate, value: any) => update({[field]: value} as Partial<Aggregate>);

    // --- Linked Licences (string[], affects Id) ---
    const handleAddLinkedLicence = () => update({linkedLicences: [...linkedLicences, '']});
    const handleLinkedLicenceChange = (index: number, value: string) => {
        const next = [...linkedLicences];
        next[index] = value;
        update({linkedLicences: next});
    };
    const handleRemoveLinkedLicence = (index: number) =>
        update({linkedLicences: linkedLicences.filter((_, i) => i !== index)});

    // --- ContainedIn ---
    const handleAddContainedIn = () => {
        setErrors({});
        const newSection = new ContainedInInformation({
            source: InformationSource.Document,
            sectionName: '',
            linkReason: ''
        });
        update({containedIn: [...containedIn, newSection]});
    };
    const handleContainedInChange = (index: number, field: keyof ContainedInInformation, value: any) => {
        const next = [...containedIn];
        next[index] = new ContainedInInformation({...next[index], [field]: value});
        update({containedIn: next});
    };
    const handleRemoveContainedIn = (index: number) =>
        update({containedIn: containedIn.filter((_, i) => i !== index)});

    // --- Points (top-level) ---
    const handleAddPoint = () => update({points: [...points, new Point({id: '', description: ''})]});
    const handlePointChange = (index: number, field: keyof Point, value: any) => {
        const next = [...points];
        next[index] = new Point({...next[index], [field]: value});
        update({points: next});
    };
    const handleRemovePoint = (index: number) => update({points: points.filter((_, i) => i !== index)});

    // --- Purposes (top-level) ---
    const handleAddPurpose = () => update({purposes: [...purposes, new Purpose({id: '', description: ''})]});
    const handlePurposeChange = (index: number, field: keyof Purpose, value: any) => {
        const next = [...purposes];
        next[index] = new Purpose({...next[index], [field]: value});
        update({purposes: next});
    };
    const handleRemovePurpose = (index: number) => update({purposes: purposes.filter((_, i) => i !== index)});

    // --- Limits (each with its own nested points/purposes) ---
    const handleAddLimit = () => update({
        limits: [...limits, new AbstractionLimit({periodType: LimitPeriodType.Unknown, points: [], purposes: []})]
    });
    const handleLimitChange = (index: number, field: keyof AbstractionLimit, value: any) => {
        const next = [...limits];
        next[index] = new AbstractionLimit({...next[index], [field]: value});
        update({limits: next});
    };
    const handleRemoveLimit = (index: number) => update({limits: limits.filter((_, i) => i !== index)});

    const handleAddLimitPoint = (limitIndex: number) => {
        const limitPoints = (limits[limitIndex].points ?? []) as Point[];
        handleLimitChange(limitIndex, 'points', [...limitPoints, new Point({id: '', description: ''})]);
    };
    const handleLimitPointChange = (limitIndex: number, pointIndex: number, field: keyof Point, value: any) => {
        const limitPoints = [...((limits[limitIndex].points ?? []) as Point[])];
        limitPoints[pointIndex] = new Point({...limitPoints[pointIndex], [field]: value});
        handleLimitChange(limitIndex, 'points', limitPoints);
    };
    const handleRemoveLimitPoint = (limitIndex: number, pointIndex: number) => {
        const limitPoints = ((limits[limitIndex].points ?? []) as Point[]).filter((_, i) => i !== pointIndex);
        handleLimitChange(limitIndex, 'points', limitPoints);
    };

    const handleAddLimitPurpose = (limitIndex: number) => {
        const limitPurposes = (limits[limitIndex].purposes ?? []) as Purpose[];
        handleLimitChange(limitIndex, 'purposes', [...limitPurposes, new Purpose({id: '', description: ''})]);
    };
    const handleLimitPurposeChange = (limitIndex: number, purposeIndex: number, field: keyof Purpose, value: any) => {
        const limitPurposes = [...((limits[limitIndex].purposes ?? []) as Purpose[])];
        limitPurposes[purposeIndex] = new Purpose({...limitPurposes[purposeIndex], [field]: value});
        handleLimitChange(limitIndex, 'purposes', limitPurposes);
    };
    const handleRemoveLimitPurpose = (limitIndex: number, purposeIndex: number) => {
        const limitPurposes = ((limits[limitIndex].purposes ?? []) as Purpose[]).filter((_, i) => i !== purposeIndex);
        handleLimitChange(limitIndex, 'purposes', limitPurposes);
    };

    // --- TimePeriod / TimeCutoff ---
    const handleTimePeriodChange = (field: keyof TimePeriod, value: any) =>
        update({timePeriod: new TimePeriod({...aggregate.timePeriod, [field]: value})});
    const handleTimeCutoffChange = (field: keyof TimeCutoff, value: any) =>
        update({timeCutoff: new TimeCutoff({...aggregate.timeCutoff, [field]: value})});

    const handleEdit = () => {
        const newErrors: Record<string, string> = {};

        if (!aggregate.primaryType || aggregate.primaryType === PrimaryType.NotSet) {
            newErrors.primaryType = 'Primary Type is required';
        }
        if (!aggregate.subType || aggregate.subType === NullableOfSubType.NotSet) {
            newErrors.subType = 'Sub Type is required';
        }

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
            <div className="aggregate-item-edit" style={{
                padding: '16px',
                border: '1px solid #d9d9d9',
                borderRadius: '4px',
                marginBottom: '16px',
                backgroundColor: '#fafafa'
            }}>
                <p style={{margin: '0 0 16px 0', fontSize: '0.85rem', color: '#555'}}>
                    <strong>Source Licence:</strong> {aggregate.sourceLicenceNumber || 'N/A'} (version {aggregate.sourceLicenceVersionId || 'N/A'})
                    <span style={{marginLeft: '8px', color: '#999'}}>— taken from this licence, not editable</span>
                </p>

                <div style={rowStyle}>
                    <div>
                        <label style={labelStyle}>Primary Type:</label>
                        <select
                            value={aggregate.primaryType ?? PrimaryType.NotSet}
                            onChange={(e) => handleChange('primaryType', e.target.value as PrimaryType)}
                            style={{...inputStyle, borderColor: errors.primaryType ? '#ff4d4f' : '#d9d9d9'}}
                        >
                            {Object.values(PrimaryType).map(v => <option key={v} value={v}>{v}</option>)}
                        </select>
                        <ValidationError message={errors.primaryType}/>
                    </div>
                    <div>
                        <label style={labelStyle}>Sub Type:</label>
                        <select
                            value={aggregate.subType ?? NullableOfSubType.NotSet}
                            onChange={(e) => handleChange('subType', e.target.value as NullableOfSubType)}
                            style={{...inputStyle, borderColor: errors.subType ? '#ff4d4f' : '#d9d9d9'}}
                        >
                            {Object.values(NullableOfSubType).map(v => <option key={v} value={v}>{v}</option>)}
                        </select>
                        <ValidationError message={errors.subType}/>
                    </div>
                    <div>
                        <label style={labelStyle}>NALD Type:</label>
                        <input type="text" value={aggregate.naldType || ''}
                               onChange={(e) => handleChange('naldType', e.target.value)} style={inputStyle}/>
                    </div>
                    <div>
                        <label style={labelStyle}>Document Identifier:</label>
                        <input type="text" value={aggregate.documentIdentifier || ''}
                               onChange={(e) => handleChange('documentIdentifier', e.target.value)}
                               style={inputStyle}/>
                    </div>
                    <div style={{paddingBottom: '6px'}}>
                        <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                            <input type="checkbox" checked={!!aggregate.isExplicitlyAggregate}
                                   onChange={(e) => handleChange('isExplicitlyAggregate', e.target.checked)}
                                   style={{marginRight: '6px'}}/>
                            Explicitly Aggregate
                        </label>
                    </div>
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Linked Licences:</strong>
                        <button onClick={handleAddLinkedLicence} style={addButtonStyle}>+ Add Linked Licence</button>
                    </div>
                    {linkedLicences.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {linkedLicences.map((ll, idx) => (
                        <div key={idx} style={{display: 'flex', gap: '8px', marginBottom: '6px'}}>
                            <input type="text" value={ll}
                                   onChange={(e) => handleLinkedLicenceChange(idx, e.target.value)}
                                   style={{...inputStyle, flex: 1}}/>
                            <button onClick={() => handleRemoveLinkedLicence(idx)} style={removeButtonStyle}>Remove</button>
                        </div>
                    ))}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Time Period:</strong>
                    </div>
                    <div style={rowStyle}>
                        <div>
                            <label style={labelStyle}>Start Date:</label>
                            <input type="date" value={aggregate.timePeriod?.startDate?.substring(0, 10) || ''}
                                   onChange={(e) => handleTimePeriodChange('startDate', e.target.value)} style={inputStyle}/>
                        </div>
                        <div>
                            <label style={labelStyle}>End Date:</label>
                            <input type="date" value={aggregate.timePeriod?.endDate?.substring(0, 10) || ''}
                                   onChange={(e) => handleTimePeriodChange('endDate', e.target.value)} style={inputStyle}/>
                        </div>
                        <div style={{paddingBottom: '6px'}}>
                            <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                                <input type="checkbox" checked={!!aggregate.timePeriod?.inclusive}
                                       onChange={(e) => handleTimePeriodChange('inclusive', e.target.checked)}
                                       style={{marginRight: '6px'}}/>
                                Inclusive
                            </label>
                        </div>
                    </div>
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Time Cutoff:</strong>
                    </div>
                    <div style={rowStyle}>
                        <div>
                            <label style={labelStyle}>Cutoff Type:</label>
                            <select value={aggregate.timeCutoff?.cutoffType ?? NullableOfCutoffType.NotApplicable}
                                    onChange={(e) => handleTimeCutoffChange('cutoffType', e.target.value as NullableOfCutoffType)}
                                    style={inputStyle}>
                                {Object.values(NullableOfCutoffType).map(v => <option key={v} value={v}>{v}</option>)}
                            </select>
                        </div>
                        <div>
                            <label style={labelStyle}>Date:</label>
                            <input type="date" value={aggregate.timeCutoff?.date?.substring(0, 10) || ''}
                                   onChange={(e) => handleTimeCutoffChange('date', e.target.value)} style={inputStyle}/>
                        </div>
                    </div>
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Contained In:</strong>
                        <button onClick={handleAddContainedIn} style={addButtonStyle}>+ Add Section</button>
                    </div>
                    {containedIn.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {containedIn.map((section, idx) => (
                        <div key={idx} style={cardStyle}>
                            <div style={rowStyle}>
                                <div>
                                    <label style={labelStyle}>Source:</label>
                                    <select value={section.source ?? InformationSource.Document}
                                            onChange={(e) => handleContainedInChange(idx, 'source', e.target.value as InformationSource)}
                                            style={inputStyle}>
                                        {Object.values(InformationSource).map(v => <option key={v} value={v}>{v}</option>)}
                                    </select>
                                </div>
                                <div>
                                    <label style={labelStyle}>Section Name:</label>
                                    <input type="text" value={section.sectionName || ''}
                                           onChange={(e) => handleContainedInChange(idx, 'sectionName', e.target.value)}
                                           style={inputStyle}/>
                                </div>
                                <div>
                                    <label style={labelStyle}>Link Reason:</label>
                                    <input type="text" value={section.linkReason || ''}
                                           onChange={(e) => handleContainedInChange(idx, 'linkReason', e.target.value)}
                                           style={inputStyle}/>
                                </div>
                                <div>
                                    <label style={labelStyle}>ACIN Code:</label>
                                    <input type="text" value={section.acinCode || ''}
                                           onChange={(e) => handleContainedInChange(idx, 'acinCode', e.target.value)}
                                           style={inputStyle}/>
                                </div>
                                <div style={{display: 'flex', justifyContent: 'flex-end'}}>
                                    <button onClick={() => handleRemoveContainedIn(idx)} style={removeButtonStyle}>Remove</button>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Points:</strong>
                        <button onClick={handleAddPoint} style={addButtonStyle}>+ Add Point</button>
                    </div>
                    {points.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {points.map((point, idx) => (
                        <div key={idx} style={{display: 'flex', gap: '8px', marginBottom: '6px', alignItems: 'center'}}>
                            <input type="text" placeholder="Id" value={point.id || ''}
                                   onChange={(e) => handlePointChange(idx, 'id', e.target.value)} style={{...inputStyle, flex: '0 0 100px'}}/>
                            <input type="text" placeholder="Description" value={point.description || ''}
                                   onChange={(e) => handlePointChange(idx, 'description', e.target.value)} style={{...inputStyle, flex: 1}}/>
                            <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', whiteSpace: 'nowrap'}}>
                                <input type="checkbox" checked={!!point.isImplicit}
                                       onChange={(e) => handlePointChange(idx, 'isImplicit', e.target.checked)}
                                       style={{marginRight: '4px'}}/>
                                Implicit
                            </label>
                            <button onClick={() => handleRemovePoint(idx)} style={removeButtonStyle}>Remove</button>
                        </div>
                    ))}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Purposes:</strong>
                        <button onClick={handleAddPurpose} style={addButtonStyle}>+ Add Purpose</button>
                    </div>
                    {purposes.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {purposes.map((purpose, idx) => (
                        <div key={idx} style={{display: 'flex', gap: '8px', marginBottom: '6px', alignItems: 'center'}}>
                            <input type="text" placeholder="Id" value={purpose.id || ''}
                                   onChange={(e) => handlePurposeChange(idx, 'id', e.target.value)} style={{...inputStyle, flex: '0 0 100px'}}/>
                            <input type="text" placeholder="Description" value={purpose.description || ''}
                                   onChange={(e) => handlePurposeChange(idx, 'description', e.target.value)} style={{...inputStyle, flex: 1}}/>
                            <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', whiteSpace: 'nowrap'}}>
                                <input type="checkbox" checked={!!purpose.isImplicit}
                                       onChange={(e) => handlePurposeChange(idx, 'isImplicit', e.target.checked)}
                                       style={{marginRight: '4px'}}/>
                                Implicit
                            </label>
                            <button onClick={() => handleRemovePurpose(idx)} style={removeButtonStyle}>Remove</button>
                        </div>
                    ))}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Limits:</strong>
                        <button onClick={handleAddLimit} style={addButtonStyle}>+ Add Limit</button>
                    </div>
                    {limits.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {limits.map((limit, limitIdx) => {
                        const limitPoints = (limit.points ?? []) as Point[];
                        const limitPurposes = (limit.purposes ?? []) as Purpose[];
                        return (
                            <div key={limitIdx} style={cardStyle}>
                                <div style={rowStyle}>
                                    <div>
                                        <label style={labelStyle}>Period Type:</label>
                                        <select value={limit.periodType ?? LimitPeriodType.Unknown}
                                                onChange={(e) => handleLimitChange(limitIdx, 'periodType', e.target.value as LimitPeriodType)}
                                                style={inputStyle}>
                                            {Object.values(LimitPeriodType).map(v => <option key={v} value={v}>{v}</option>)}
                                        </select>
                                    </div>
                                    <div>
                                        <label style={labelStyle}>Value:</label>
                                        <input type="number" value={limit.value ?? ''}
                                               onChange={(e) => handleLimitChange(limitIdx, 'value', e.target.value === '' ? undefined : Number(e.target.value))}
                                               style={inputStyle}/>
                                    </div>
                                    <div>
                                        <label style={labelStyle}>Units:</label>
                                        <input type="text" value={limit.units || ''}
                                               onChange={(e) => handleLimitChange(limitIdx, 'units', e.target.value)}
                                               style={inputStyle}/>
                                    </div>
                                    <div>
                                        <label style={labelStyle}>Additional Text:</label>
                                        <input type="text" value={limit.valueAdditionalText || ''}
                                               onChange={(e) => handleLimitChange(limitIdx, 'valueAdditionalText', e.target.value)}
                                               style={inputStyle}/>
                                    </div>
                                    <div style={{paddingBottom: '6px'}}>
                                        <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                                            <input type="checkbox" checked={!!limit.implicitLimit}
                                                   onChange={(e) => handleLimitChange(limitIdx, 'implicitLimit', e.target.checked)}
                                                   style={{marginRight: '6px'}}/>
                                            Implicit
                                        </label>
                                    </div>
                                    <div style={{paddingBottom: '6px'}}>
                                        <label style={{fontSize: '0.75rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                                            <input type="checkbox" checked={!!limit.isAverage}
                                                   onChange={(e) => handleLimitChange(limitIdx, 'isAverage', e.target.checked)}
                                                   style={{marginRight: '6px'}}/>
                                            Is Average
                                        </label>
                                    </div>
                                    <div>
                                        <label style={labelStyle}>Average Period (days):</label>
                                        <input type="number" value={limit.averagePeriod ?? ''}
                                               onChange={(e) => handleLimitChange(limitIdx, 'averagePeriod', e.target.value === '' ? undefined : Number(e.target.value))}
                                               style={inputStyle}/>
                                    </div>
                                    <div style={{display: 'flex', justifyContent: 'flex-end'}}>
                                        <button onClick={() => handleRemoveLimit(limitIdx)} style={removeButtonStyle}>Remove Limit</button>
                                    </div>
                                </div>

                                <div style={{marginTop: '10px'}}>
                                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px'}}>
                                        <span style={{fontSize: '0.8rem', fontWeight: 600}}>Limit Points:</span>
                                        <button onClick={() => handleAddLimitPoint(limitIdx)} style={addButtonStyle}>+ Add</button>
                                    </div>
                                    {limitPoints.map((point, pointIdx) => (
                                        <div key={pointIdx} style={{display: 'flex', gap: '8px', marginBottom: '6px', alignItems: 'center'}}>
                                            <input type="text" placeholder="Id" value={point.id || ''}
                                                   onChange={(e) => handleLimitPointChange(limitIdx, pointIdx, 'id', e.target.value)}
                                                   style={{...inputStyle, flex: '0 0 100px'}}/>
                                            <input type="text" placeholder="Description" value={point.description || ''}
                                                   onChange={(e) => handleLimitPointChange(limitIdx, pointIdx, 'description', e.target.value)}
                                                   style={{...inputStyle, flex: 1}}/>
                                            <button onClick={() => handleRemoveLimitPoint(limitIdx, pointIdx)} style={removeButtonStyle}>Remove</button>
                                        </div>
                                    ))}
                                </div>

                                <div style={{marginTop: '10px'}}>
                                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px'}}>
                                        <span style={{fontSize: '0.8rem', fontWeight: 600}}>Limit Purposes:</span>
                                        <button onClick={() => handleAddLimitPurpose(limitIdx)} style={addButtonStyle}>+ Add</button>
                                    </div>
                                    {limitPurposes.map((purpose, purposeIdx) => (
                                        <div key={purposeIdx} style={{display: 'flex', gap: '8px', marginBottom: '6px', alignItems: 'center'}}>
                                            <input type="text" placeholder="Id" value={purpose.id || ''}
                                                   onChange={(e) => handleLimitPurposeChange(limitIdx, purposeIdx, 'id', e.target.value)}
                                                   style={{...inputStyle, flex: '0 0 100px'}}/>
                                            <input type="text" placeholder="Description" value={purpose.description || ''}
                                                   onChange={(e) => handleLimitPurposeChange(limitIdx, purposeIdx, 'description', e.target.value)}
                                                   style={{...inputStyle, flex: 1}}/>
                                            <button onClick={() => handleRemoveLimitPurpose(limitIdx, purposeIdx)} style={removeButtonStyle}>Remove</button>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        );
                    })}
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

    const itemId = computeAggregateId(aggregate);

    return (
        <div className="aggregate-item" style={{padding: '12px', borderBottom: '1px solid #eee'}}>
            <div style={{display: 'flex', gap: '24px', flexWrap: 'wrap', marginBottom: '8px'}}>
                <p style={{margin: 0}}><strong>Source Licence:</strong> {aggregate.sourceLicenceNumber || 'N/A'} (v{aggregate.sourceLicenceVersionId || 'N/A'})</p>
                <p style={{margin: 0}}><strong>Type:</strong> {aggregate.primaryType || 'N/A'} / {aggregate.subType || 'N/A'}</p>
                <p style={{margin: 0}}><strong>NALD Type:</strong> {aggregate.naldType || 'N/A'}</p>
                <p style={{margin: 0}}><strong>Limits:</strong> {limits.length}</p>
            </div>
            {linkedLicences.length > 0 && (
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem'}}>
                    <strong>Linked Licences:</strong> {linkedLicences.join(', ')}
                </p>
            )}
            {containedIn.length > 0 && (
                <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
                    <strong style={{display: 'block', marginBottom: '8px'}}>Contained In:</strong>
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {containedIn.map((section, idx) => (
                            <li key={idx} style={{marginBottom: '8px', padding: '8px', backgroundColor: '#f9f9f9', borderRadius: '4px'}}>
                                <div style={{display: 'flex', flexWrap: 'wrap', gap: '8px 16px', alignItems: 'center'}}>
                                    <div><strong>Source:</strong> {section.source || 'N/A'}</div>
                                    <div><strong>Section:</strong> {section.sectionName || 'N/A'}</div>
                                    <div><strong>Link Reason:</strong> {section.linkReason || 'N/A'}</div>
                                    {section.pageNumber !== undefined && section.pageNumber !== null && section.pageNumber > 0 && (
                                        <button
                                            onClick={() => onJumpToPage && onJumpToPage(section.pageNumber!)}
                                            title={`Jump to page ${section.pageNumber}`}
                                            style={{
                                                background: 'none', border: '1px solid #d9d9d9', borderRadius: '4px',
                                                cursor: 'pointer', fontSize: '0.85rem', padding: '2px 6px',
                                                display: 'flex', alignItems: 'center', gap: '4px'
                                            }}
                                        >
                                            📄 <span style={{fontSize: '0.75rem'}}>Page {section.pageNumber}</span>
                                        </button>
                                    )}
                                </div>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            {!scrapedView && (onVerify || onReject || onOverride) && (
                <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginTop: '16px', gap: '16px'}}>
                    <div style={{flex: 1}}>
                        {(() => {
                            const latestVerification = (history || [])
                                .filter(v => v.licenceSectionName === 'Aggregates' && v.licenceSectionItemId === itemId)
                                .sort((a, b) => {
                                    const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
                                    const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
                                    return dateB - dateA;
                                })[0];

                            if (!latestVerification) return null;

                            return <LicenceSectionVerificationInfo verification={latestVerification}/>;
                        })()}
                    </div>
                    <div style={{display: 'flex', flexDirection: 'column', gap: '8px', alignItems: 'flex-end'}}>
                        <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                            <button onClick={onVerify} style={{padding: '4px 12px', backgroundColor: '#52c41a', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Confirm</button>
                            <button onClick={onReject} style={{padding: '4px 12px', backgroundColor: '#ff4d4f', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Remove</button>
                            <button onClick={onOverride} style={{padding: '4px 12px', backgroundColor: '#1890ff', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Edit</button>
                        </div>
                        <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                            <button onClick={onRequestBusinessReview} style={{padding: '4px 12px', backgroundColor: 'darkorange', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Request Business Review</button>
                            <button onClick={onCompleteBusinessReview} style={{padding: '4px 12px', backgroundColor: 'purple', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Complete Business Review</button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
