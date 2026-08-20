import React from "react";
import {ContainedInInformation} from "../../api/generated/apiClient.ts";
import {ValidationError} from "./ValidationError.tsx";

const labelStyle: React.CSSProperties = {display: 'block', fontSize: '0.75rem', marginBottom: '4px', fontWeight: 600};
const inputStyle: React.CSSProperties = {width: '100%', height: '30px', padding: '4px 8px', border: '1px solid #d9d9d9', borderRadius: '4px', boxSizing: 'border-box'};
const rowStyle: React.CSSProperties = {display: 'flex', gap: '12px', alignItems: 'end', flexWrap: 'nowrap'};
const cardStyle: React.CSSProperties = {marginBottom: '10px', padding: '10px', border: '1px solid #eee', borderRadius: '4px', backgroundColor: 'white'};
const removeButtonStyle: React.CSSProperties = {height: '30px', padding: '4px 8px', fontSize: '0.75rem', backgroundColor: '#ff7875', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', boxSizing: 'border-box'};
const removeButtonDisabledStyle: React.CSSProperties = {...removeButtonStyle, backgroundColor: '#f5f5f5', color: 'rgba(0, 0, 0, 0.25)', border: '1px solid #d9d9d9', cursor: 'not-allowed'};

interface ContainedInEditProps {
    sections: ContainedInInformation[];
    onChange: (index: number, field: keyof ContainedInInformation, value: any) => void;
    onRemove: (index: number) => void;
    onJumpToPage?: (pageNumber: number) => void;
    showLinkReason?: boolean;
    canRemove?: (index: number) => boolean;
    getFieldError?: (index: number, field: 'sectionName' | 'linkReason') => string | undefined;
}

export const ContainedInEdit = ({
                                     sections,
                                     onChange,
                                     onRemove,
                                     onJumpToPage,
                                     showLinkReason,
                                     canRemove,
                                     getFieldError
                                 }: ContainedInEditProps) => {
    return (
        <>
            {sections.map((section, idx) => {
                const removable = canRemove ? canRemove(idx) : true;
                return (
                    <div key={idx} style={cardStyle}>
                        <div style={rowStyle}>
                            <div style={{flex: 1, minWidth: 0}}>
                                <label style={labelStyle}>Source:</label>
                                <input type="text" value={section.source || ''} readOnly
                                       style={{...inputStyle, backgroundColor: '#f0f0f0'}}/>
                                <ValidationError message={undefined}/>
                            </div>
                            <div style={{flex: 1, minWidth: 0}}>
                                <label style={labelStyle}>Section Name:</label>
                                <input type="text" value={section.sectionName || ''}
                                       onChange={(e) => onChange(idx, 'sectionName', e.target.value)}
                                       style={{...inputStyle, borderColor: getFieldError?.(idx, 'sectionName') ? '#ff4d4f' : '#d9d9d9'}}/>
                                <ValidationError message={getFieldError?.(idx, 'sectionName')}/>
                            </div>
                            {showLinkReason && (
                                <div style={{flex: 1, minWidth: 0}}>
                                    <label style={labelStyle}>Link Reason:</label>
                                    <input type="text" value={section.linkReason || ''}
                                           onChange={(e) => onChange(idx, 'linkReason', e.target.value)}
                                           style={{...inputStyle, borderColor: getFieldError?.(idx, 'linkReason') ? '#ff4d4f' : '#d9d9d9'}}/>
                                    <ValidationError message={getFieldError?.(idx, 'linkReason')}/>
                                </div>
                            )}
                            {section.pageNumber !== undefined && section.pageNumber !== null && section.pageNumber > 0 && (
                                <div style={{flex: '0 0 auto'}}>
                                    <button
                                        onClick={(e) => {
                                            e.stopPropagation();
                                            onJumpToPage && onJumpToPage(section.pageNumber!);
                                        }}
                                        title={`Jump to page ${section.pageNumber}`}
                                        style={{
                                            background: '#f0f0f0', border: '1px solid #d9d9d9', borderRadius: '4px',
                                            cursor: 'pointer', fontSize: '0.85rem', padding: '2px 6px',
                                            height: '30px', boxSizing: 'border-box',
                                            display: 'flex', alignItems: 'center', gap: '4px'
                                        }}
                                    >
                                        📄 <span style={{fontSize: '0.75rem'}}>Page {section.pageNumber}</span>
                                    </button>
                                    <ValidationError message={undefined}/>
                                </div>
                            )}
                            <div style={{flex: '0 0 auto'}}>
                                <button onClick={() => onRemove(idx)} disabled={!removable}
                                        style={{...(removable ? removeButtonStyle : removeButtonDisabledStyle), whiteSpace: 'nowrap'}}>
                                    Remove
                                </button>
                                <ValidationError message={undefined}/>
                            </div>
                        </div>
                    </div>
                );
            })}
        </>
    );
};
