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
import {ValidationError} from "../ValidationError.tsx";
import {ContainedInList} from "../ContainedInList.tsx";
import {ContainedInEdit} from "../ContainedInEdit.tsx";
import {VerificationActions} from "../VerificationActions.tsx";
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
const topLevelLabelStyle: React.CSSProperties = {display: 'block', fontSize: '0.9rem', marginBottom: '4px', fontWeight: 600};
const inputStyle: React.CSSProperties = {width: '100%', height: '30px', padding: '4px 8px', border: '1px solid #d9d9d9', borderRadius: '4px', boxSizing: 'border-box'};
const rowStyle: React.CSSProperties = {display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: '12px', alignItems: 'end'};
const cardStyle: React.CSSProperties = {marginBottom: '10px', padding: '10px', border: '1px solid #eee', borderRadius: '4px', backgroundColor: 'white'};
const addButtonStyle: React.CSSProperties = {padding: '4px 12px', fontSize: '0.8rem', backgroundColor: '#52c41a', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer'};
const removeButtonStyle: React.CSSProperties = {height: '30px', padding: '4px 8px', fontSize: '0.75rem', backgroundColor: '#ff7875', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', boxSizing: 'border-box'};
const removeButtonDisabledStyle: React.CSSProperties = {...removeButtonStyle, backgroundColor: '#f5f5f5', color: 'rgba(0, 0, 0, 0.25)', border: '1px solid #d9d9d9', cursor: 'not-allowed'};

const LIMIT_UNITS_OPTIONS = [
    'megalitres', 'litres', 'thousand cubic metres', 'cubic metres',
    'megagallons', 'thousand gallons', 'million gallons', 'gallons'
];

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
    const [showTimePeriod, setShowTimePeriod] = React.useState<boolean>(!!aggregateProp?.timePeriod);
    const [showTimeCutoff, setShowTimeCutoff] = React.useState<boolean>(!!aggregateProp?.timeCutoff);
    const aggregate = aggregateProp;

    // AggregateItem is not remounted between edit sessions (stable key={index} in Aggregates.tsx's
    // list, and discard restores data into the same slot), so plain useState initializers only run
    // once. Re-derive the show/hide toggles — and clear stale errors — every time editing (re)starts.
    React.useEffect(() => {
        if (isEditing) {
            setShowTimePeriod(!!aggregate?.timePeriod);
            setShowTimeCutoff(!!aggregate?.timeCutoff);
            setErrors({});
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isEditing]);

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
        limits: [...limits, new AbstractionLimit({points: [], purposes: []})]
    });
    const handleLimitChange = (index: number, field: keyof AbstractionLimit, value: any) => {
        const next = [...limits];
        next[index] = new AbstractionLimit({...next[index], [field]: value});
        update({limits: next});
    };
    const handleRemoveLimit = (index: number) => update({limits: limits.filter((_, i) => i !== index)});

    // --- TimePeriod / TimeCutoff ---
    const handleTimePeriodChange = (field: keyof TimePeriod, value: any) =>
        update({timePeriod: new TimePeriod({...aggregate.timePeriod, [field]: value})});
    const handleTimeCutoffChange = (field: keyof TimeCutoff, value: any) =>
        update({timeCutoff: new TimeCutoff({...aggregate.timeCutoff, [field]: value})});

    const handleShowTimePeriodToggle = (checked: boolean) => {
        setShowTimePeriod(checked);
        if (checked && !aggregate.timePeriod) {
            update({timePeriod: new TimePeriod({startDate: '', endDate: '', inclusive: false})});
        }
        // unchecking: don't touch aggregate.timePeriod here — only nulled at save time in handleEdit
    };
    const handleShowTimeCutoffToggle = (checked: boolean) => {
        setShowTimeCutoff(checked);
        if (checked && !aggregate.timeCutoff) {
            update({timeCutoff: new TimeCutoff({cutoffType: NullableOfCutoffType.NotApplicable, date: ''})});
        }
        // unchecking: don't touch aggregate.timeCutoff here — only nulled at save time in handleEdit
    };

    const handleEdit = () => {
        const newErrors: Record<string, string> = {};

        if (!aggregate.primaryType || aggregate.primaryType === PrimaryType.NotSet) {
            newErrors.primaryType = 'Primary Type is required';
        }
        // Sub Type is optional — not validated.

        linkedLicences.forEach((ll, idx) => {
            if (!ll || !ll.trim()) newErrors[`linkedLicence_${idx}`] = 'Linked Licence cannot be empty';
        });

        if (showTimePeriod) {
            if (!aggregate.timePeriod?.startDate) newErrors.timePeriodStartDate = 'Start Date is required';
            if (!aggregate.timePeriod?.endDate) newErrors.timePeriodEndDate = 'End Date is required';
        }
        if (showTimeCutoff) {
            if (!aggregate.timeCutoff?.cutoffType) newErrors.timeCutoffType = 'Cutoff Type is required';
            if (!aggregate.timeCutoff?.date) newErrors.timeCutoffDate = 'Date is required';
        }

        limits.forEach((limit, idx) => {
            if (!limit.periodType) newErrors[`limit_${idx}_periodType`] = 'Period Type is required';
            if (limit.value === undefined || limit.value === null || Number.isNaN(limit.value)) {
                newErrors[`limit_${idx}_value`] = 'Value is required';
            }
            if (!limit.units) newErrors[`limit_${idx}_units`] = 'Units is required';
        });

        containedIn.forEach((section, idx) => {
            if (!section.sectionName || !section.sectionName.trim()) {
                newErrors[`containedIn_${idx}_sectionName`] = 'Section Name is required';
            }
        });

        points.forEach((point, idx) => {
            if (!(point.id?.trim() || point.altId?.trim() || point.description?.trim())) {
                newErrors[`point_${idx}`] = 'At least one of Id, AltId or Description is required';
            }
        });

        purposes.forEach((purpose, idx) => {
            if (!(purpose.id?.trim() || purpose.description?.trim())) {
                newErrors[`purpose_${idx}`] = 'At least one of Id or Description is required';
            }
        });

        setErrors(newErrors);
        if (Object.keys(newErrors).length > 0) {
            return;
        }

        // Null out hidden groups only now, at save time — never reactively on uncheck,
        // which would lose data if the user re-checks the box before saving.
        if (!showTimePeriod || !showTimeCutoff) {
            update({
                ...(!showTimePeriod ? {timePeriod: undefined} : {}),
                ...(!showTimeCutoff ? {timeCutoff: undefined} : {}),
            });
        }

        if (onOverride) {
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
                </p>

                <div style={rowStyle}>
                    <div>
                        <label style={topLevelLabelStyle}>Primary Type:</label>
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
                        <label style={topLevelLabelStyle}>Sub Type:</label>
                        <select
                            value={aggregate.subType ?? NullableOfSubType.NotSet}
                            onChange={(e) => handleChange('subType', e.target.value as NullableOfSubType)}
                            style={{...inputStyle, borderColor: errors.subType ? '#ff4d4f' : '#d9d9d9'}}
                        >
                            {Object.values(NullableOfSubType).map(v => <option key={v} value={v}>{v}</option>)}
                        </select>
                        <ValidationError message={errors.subType}/>
                    </div>
                </div>

                <div style={{...rowStyle, marginTop: '12px'}}>
                    <div>
                        <label style={topLevelLabelStyle}>Document Identifier:</label>
                        <input type="text" value={aggregate.documentIdentifier || ''}
                               onChange={(e) => handleChange('documentIdentifier', e.target.value)}
                               style={inputStyle}/>
                    </div>
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Linked Licences:</strong>
                        <button onClick={handleAddLinkedLicence} style={addButtonStyle}>+ Add Linked Licence</button>
                    </div>
                    {linkedLicences.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {linkedLicences.map((ll, idx) => (
                        <div key={idx} style={cardStyle}>
                            <div style={{display: 'flex', gap: '8px', alignItems: 'end'}}>
                                <div style={{flex: 1}}>
                                    <label style={labelStyle}>Licence Number:</label>
                                    <input type="text" value={ll}
                                           onChange={(e) => handleLinkedLicenceChange(idx, e.target.value)}
                                           style={{...inputStyle, borderColor: errors[`linkedLicence_${idx}`] ? '#ff4d4f' : '#d9d9d9'}}/>
                                </div>
                                <button onClick={() => handleRemoveLinkedLicence(idx)} disabled={linkedLicences.length <= 1}
                                        style={linkedLicences.length <= 1 ? removeButtonDisabledStyle : removeButtonStyle}>Remove</button>
                            </div>
                            <ValidationError message={errors[`linkedLicence_${idx}`]}/>
                        </div>
                    ))}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Time Period:</strong>
                        <label style={{fontSize: '0.8rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                            <input type="checkbox" checked={showTimePeriod}
                                   onChange={(e) => handleShowTimePeriodToggle(e.target.checked)}
                                   style={{marginRight: '6px'}}/>
                            Include Time Period
                        </label>
                    </div>
                    {showTimePeriod && (
                        <div style={cardStyle}>
                            <div style={rowStyle}>
                                <div>
                                    <label style={labelStyle}>Start Date:</label>
                                    <input type="date" value={aggregate.timePeriod?.startDate?.substring(0, 10) || ''}
                                           onChange={(e) => handleTimePeriodChange('startDate', e.target.value)}
                                           style={{...inputStyle, borderColor: errors.timePeriodStartDate ? '#ff4d4f' : '#d9d9d9'}}/>
                                    <ValidationError message={errors.timePeriodStartDate}/>
                                </div>
                                <div>
                                    <label style={labelStyle}>End Date:</label>
                                    <input type="date" value={aggregate.timePeriod?.endDate?.substring(0, 10) || ''}
                                           onChange={(e) => handleTimePeriodChange('endDate', e.target.value)}
                                           style={{...inputStyle, borderColor: errors.timePeriodEndDate ? '#ff4d4f' : '#d9d9d9'}}/>
                                    <ValidationError message={errors.timePeriodEndDate}/>
                                </div>
                                <div>
                                    <label style={{fontSize: '0.75rem', height: '30px', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                                        <input type="checkbox" checked={!!aggregate.timePeriod?.inclusive}
                                               onChange={(e) => handleTimePeriodChange('inclusive', e.target.checked)}
                                               style={{marginRight: '6px'}}/>
                                        Inclusive
                                    </label>
                                    <ValidationError message={undefined}/>
                                </div>
                            </div>
                        </div>
                    )}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Time Cutoff:</strong>
                        <label style={{fontSize: '0.8rem', display: 'flex', alignItems: 'center', cursor: 'pointer'}}>
                            <input type="checkbox" checked={showTimeCutoff}
                                   onChange={(e) => handleShowTimeCutoffToggle(e.target.checked)}
                                   style={{marginRight: '6px'}}/>
                            Include Time Cutoff
                        </label>
                    </div>
                    {showTimeCutoff && (
                        <div style={cardStyle}>
                            <div style={rowStyle}>
                                <div>
                                    <label style={labelStyle}>Cutoff Type:</label>
                                    <select value={aggregate.timeCutoff?.cutoffType ?? NullableOfCutoffType.NotApplicable}
                                            onChange={(e) => handleTimeCutoffChange('cutoffType', e.target.value as NullableOfCutoffType)}
                                            style={{...inputStyle, borderColor: errors.timeCutoffType ? '#ff4d4f' : '#d9d9d9'}}>
                                        {Object.values(NullableOfCutoffType).map(v => <option key={v} value={v}>{v}</option>)}
                                    </select>
                                    <ValidationError message={errors.timeCutoffType}/>
                                </div>
                                <div>
                                    <label style={labelStyle}>Date:</label>
                                    <input type="date" value={aggregate.timeCutoff?.date?.substring(0, 10) || ''}
                                           onChange={(e) => handleTimeCutoffChange('date', e.target.value)}
                                           style={{...inputStyle, borderColor: errors.timeCutoffDate ? '#ff4d4f' : '#d9d9d9'}}/>
                                    <ValidationError message={errors.timeCutoffDate}/>
                                </div>
                            </div>
                        </div>
                    )}
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Contained In:</strong>
                        <button onClick={handleAddContainedIn} style={addButtonStyle}>+ Add Section</button>
                    </div>
                    {containedIn.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    <ContainedInEdit
                        sections={containedIn}
                        onChange={handleContainedInChange}
                        onRemove={handleRemoveContainedIn}
                        onJumpToPage={onJumpToPage}
                        canRemove={() => containedIn.length > 1}
                        getFieldError={(idx, field) => field === 'sectionName' ? errors[`containedIn_${idx}_sectionName`] : undefined}
                    />
                </div>

                <div style={{marginTop: '16px'}}>
                    <div style={{display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px'}}>
                        <strong style={{fontSize: '0.9rem'}}>Points:</strong>
                        <button onClick={handleAddPoint} style={addButtonStyle}>+ Add Point</button>
                    </div>
                    {points.length === 0 && <p style={{fontSize: '0.8rem', color: '#888'}}>None</p>}
                    {points.map((point, idx) => (
                        <div key={idx} style={cardStyle}>
                            <div style={{display: 'flex', gap: '8px', alignItems: 'end'}}>
                                <div style={{flex: '0 0 100px'}}>
                                    <label style={labelStyle}>Id:</label>
                                    <input type="text" value={point.id || ''}
                                           onChange={(e) => handlePointChange(idx, 'id', e.target.value)} style={inputStyle}/>
                                </div>
                                <div style={{flex: '0 0 100px'}}>
                                    <label style={labelStyle}>Alt Id:</label>
                                    <input type="text" value={point.altId || ''}
                                           onChange={(e) => handlePointChange(idx, 'altId', e.target.value)} style={inputStyle}/>
                                </div>
                                <div style={{flex: 1}}>
                                    <label style={labelStyle}>Description:</label>
                                    <input type="text" value={point.description || ''}
                                           onChange={(e) => handlePointChange(idx, 'description', e.target.value)} style={inputStyle}/>
                                </div>
                                <button onClick={() => handleRemovePoint(idx)} disabled={points.length <= 1}
                                        style={points.length <= 1 ? removeButtonDisabledStyle : removeButtonStyle}>Remove</button>
                            </div>
                            <ValidationError message={errors[`point_${idx}`]}/>
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
                        <div key={idx} style={cardStyle}>
                            <div style={{display: 'flex', gap: '8px', alignItems: 'end'}}>
                                <div style={{flex: '0 0 100px'}}>
                                    <label style={labelStyle}>Id:</label>
                                    <input type="text" value={purpose.id || ''}
                                           onChange={(e) => handlePurposeChange(idx, 'id', e.target.value)} style={inputStyle}/>
                                </div>
                                <div style={{flex: 1}}>
                                    <label style={labelStyle}>Description:</label>
                                    <input type="text" value={purpose.description || ''}
                                           onChange={(e) => handlePurposeChange(idx, 'description', e.target.value)} style={inputStyle}/>
                                </div>
                                <button onClick={() => handleRemovePurpose(idx)} disabled={purposes.length <= 1}
                                        style={purposes.length <= 1 ? removeButtonDisabledStyle : removeButtonStyle}>Remove</button>
                            </div>
                            <ValidationError message={errors[`purpose_${idx}`]}/>
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
                        return (
                            <div key={limitIdx} style={cardStyle}>
                                <div style={{display: 'flex', gap: '12px', alignItems: 'end', flexWrap: 'nowrap'}}>
                                    <div style={{flex: 1, minWidth: 0}}>
                                        <label style={labelStyle}>Value:</label>
                                        <input type="number" value={limit.value ?? ''}
                                               onChange={(e) => handleLimitChange(limitIdx, 'value', e.target.value === '' ? undefined : Number(e.target.value))}
                                               style={{...inputStyle, borderColor: errors[`limit_${limitIdx}_value`] ? '#ff4d4f' : '#d9d9d9'}}/>
                                        <ValidationError message={errors[`limit_${limitIdx}_value`]}/>
                                    </div>
                                    <div style={{flex: 1, minWidth: 0}}>
                                        <label style={labelStyle}>Units:</label>
                                        <select value={limit.units || ''}
                                                onChange={(e) => handleLimitChange(limitIdx, 'units', e.target.value || undefined)}
                                                style={{...inputStyle, borderColor: errors[`limit_${limitIdx}_units`] ? '#ff4d4f' : '#d9d9d9'}}>
                                            <option value="">-- Select --</option>
                                            {LIMIT_UNITS_OPTIONS.map(u => <option key={u} value={u}>{u}</option>)}
                                        </select>
                                        <ValidationError message={errors[`limit_${limitIdx}_units`]}/>
                                    </div>
                                    <div style={{flex: 1, minWidth: 0}}>
                                        <label style={labelStyle}>Period Type:</label>
                                        <select value={limit.periodType ?? ''}
                                                onChange={(e) => handleLimitChange(limitIdx, 'periodType', e.target.value === '' ? undefined : e.target.value as LimitPeriodType)}
                                                style={{...inputStyle, borderColor: errors[`limit_${limitIdx}_periodType`] ? '#ff4d4f' : '#d9d9d9'}}>
                                            <option value="">-- Select --</option>
                                            {Object.values(LimitPeriodType).filter(v => v !== LimitPeriodType.InTotal).map(v => <option key={v} value={v}>{v}</option>)}
                                        </select>
                                        <ValidationError message={errors[`limit_${limitIdx}_periodType`]}/>
                                    </div>
                                    <div style={{flex: '0 0 auto'}}>
                                        <button onClick={() => handleRemoveLimit(limitIdx)} disabled={limits.length <= 1}
                                                style={{...(limits.length <= 1 ? removeButtonDisabledStyle : removeButtonStyle), whiteSpace: 'nowrap'}}>Remove</button>
                                        <ValidationError message={undefined}/>
                                    </div>
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
            <div style={{marginBottom: '8px'}}>
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem'}}><strong>Id:</strong> {aggregate.id || 'N/A'}</p>
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem', display: 'flex', gap: '24px'}}>
                    <span><strong>Primary Type:</strong> {aggregate.primaryType || 'N/A'}</span>
                    <span><strong>Sub Type:</strong> {aggregate.subType || 'N/A'}</span>
                </p>
                <p style={{margin: 0, fontSize: '0.9rem'}}><strong>Document Identifier:</strong> {aggregate.documentIdentifier || 'N/A'}</p>
            </div>
            {linkedLicences.length > 0 && (
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem'}}>
                    <strong>Linked Licences:</strong> {linkedLicences.join(', ')}
                </p>
            )}
            {aggregate.timePeriod && (
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem'}}>
                    <strong>Time Period:</strong> {aggregate.timePeriod.startDate?.substring(0, 10) || 'N/A'} to {aggregate.timePeriod.endDate?.substring(0, 10) || 'N/A'}
                    {aggregate.timePeriod.inclusive ? ' (Inclusive)' : ' (Exclusive)'}
                </p>
            )}
            {aggregate.timeCutoff && (
                <p style={{margin: '0 0 8px 0', fontSize: '0.9rem'}}>
                    <strong>Time Cutoff:</strong> {aggregate.timeCutoff.cutoffType || 'N/A'}
                    {aggregate.timeCutoff.date ? ` ${aggregate.timeCutoff.date.substring(0, 10)}` : ''}
                </p>
            )}
            {limits.length > 0 && (
                <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
                    <strong style={{display: 'block', marginBottom: '8px'}}>Limits:</strong>
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {limits.map((limit, idx) => (
                            <li key={idx} style={{marginBottom: '8px', padding: '8px', backgroundColor: '#f9f9f9', borderRadius: '4px'}}>
                                <div style={{display: 'flex', flexWrap: 'wrap', gap: '8px 16px', alignItems: 'center'}}>
                                    <div><strong>Value:</strong> {limit.value ?? 'N/A'}</div>
                                    <div><strong>Units:</strong> {limit.units || 'N/A'}</div>
                                    <div><strong>Period Type:</strong> {limit.periodType || 'N/A'}</div>
                                    {limit.isAverage && (
                                        <div><strong>Average Period:</strong> {limit.averagePeriod ?? 'N/A'}</div>
                                    )}
                                    {limit.implicitLimit && (
                                        <div><strong>Implicit Limit:</strong> Yes</div>
                                    )}
                                    {limit.valueAdditionalText && (
                                        <div><strong>Additional Text:</strong> {limit.valueAdditionalText}</div>
                                    )}
                                </div>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            {points.length > 0 && (
                <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
                    <strong style={{display: 'block', marginBottom: '8px'}}>Points:</strong>
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {points.map((point, idx) => (
                            <li key={idx} style={{marginBottom: '4px', padding: '6px 8px', backgroundColor: '#f9f9f9', borderRadius: '4px'}}>
                                {[point.id, point.altId, point.description].filter(v => v && v.trim()).join(' / ') || 'N/A'}
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            {purposes.length > 0 && (
                <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
                    <strong style={{display: 'block', marginBottom: '8px'}}>Purposes:</strong>
                    <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                        {purposes.map((purpose, idx) => (
                            <li key={idx} style={{marginBottom: '4px', padding: '6px 8px', backgroundColor: '#f9f9f9', borderRadius: '4px'}}>
                                {[purpose.id, purpose.description].filter(v => v && v.trim()).join(' / ') || 'N/A'}
                            </li>
                        ))}
                    </ul>
                </div>
            )}
            <ContainedInList sections={containedIn} onJumpToPage={onJumpToPage}/>
            <VerificationActions
                scrapedView={scrapedView}
                history={history}
                licenceSectionName="Aggregates"
                itemId={itemId}
                onVerify={onVerify}
                onReject={onReject}
                onOverride={onOverride}
                onRequestBusinessReview={onRequestBusinessReview}
                onCompleteBusinessReview={onCompleteBusinessReview}
            />
        </div>
    );
};
