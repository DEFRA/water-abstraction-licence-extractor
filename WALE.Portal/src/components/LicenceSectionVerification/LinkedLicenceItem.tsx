import { LinkedLicence, LinkedLicenceDirection, LinkedLicenceSection } from "../../api/generated/apiClient.ts";

interface LinkedLicenceItemProps {
    linkedLicence: LinkedLicence;
    isEditing?: boolean;
    onUpdate?: (updated: LinkedLicence) => void;
    onRemove?: () => void;
}

export const LinkedLicenceItem = ({ 
    linkedLicence, 
    isEditing, 
    onUpdate, 
    onRemove 
}: LinkedLicenceItemProps) => {
    const handleChange = (field: keyof LinkedLicence, value: any) => {
        if (onUpdate) {
            onUpdate(new LinkedLicence({ ...linkedLicence, [field]: value }));
        }
    };

    const handleSectionChange = (index: number, field: keyof LinkedLicenceSection, value: any) => {
        if (onUpdate && linkedLicence.containedIn) {
            const newSections = [...linkedLicence.containedIn];
            newSections[index] = new LinkedLicenceSection({ ...newSections[index], [field]: value });
            onUpdate(new LinkedLicence({ ...linkedLicence, containedIn: newSections }));
        }
    };

    const handleAddSection = () => {
        if (onUpdate) {
            const newSection = new LinkedLicenceSection({
                direction: LinkedLicenceDirection.Outgoing,
                sectionName: '',
                linkReason: '',
                isBecauseOfAggregate: false,
                lineNumber: 0,
                pageNumber: 0
            });
            const newSections = [...(linkedLicence.containedIn || []), newSection];
            onUpdate(new LinkedLicence({ ...linkedLicence, containedIn: newSections }));
        }
    };

    const handleRemoveSection = (index: number) => {
        if (onUpdate && linkedLicence.containedIn) {
            const newSections = linkedLicence.containedIn.filter((_, i) => i !== index);
            onUpdate(new LinkedLicence({ ...linkedLicence, containedIn: newSections }));
        }
    };

    if (isEditing) {
        return (
            <div className="linked-licence-item-edit" style={{ padding: '12px', borderBottom: '1px solid #ccc', marginBottom: '12px', backgroundColor: '#f9f9f9' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '8px' }}>
                    <div style={{ flex: 1, marginRight: '8px' }}>
                        <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 'bold' }}>Linked Licence Number:</label>
                        <input 
                            type="text" 
                            value={linkedLicence.licenceNumber || ''} 
                            onChange={(e) => handleChange('licenceNumber', e.target.value)}
                            style={{ width: '100%', padding: '4px' }}
                        />
                    </div>
                    <div style={{ flex: 1, marginRight: '8px' }}>
                        <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 'bold' }}>Permit Number:</label>
                        <input 
                            type="text" 
                            value={linkedLicence.permitNumber || ''} 
                            onChange={(e) => handleChange('permitNumber', e.target.value)}
                            style={{ width: '100%', padding: '4px' }}
                        />
                    </div>
                    <button 
                        onClick={onRemove}
                        style={{ alignSelf: 'flex-end', padding: '4px 8px', backgroundColor: '#ff4d4f', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                    >
                        Remove Licence
                    </button>
                </div>

                <div style={{ marginTop: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                        <strong style={{ fontSize: '0.9rem' }}>Contained In (Sections):</strong>
                        <button 
                            onClick={handleAddSection}
                            style={{ padding: '2px 8px', fontSize: '0.8rem', backgroundColor: '#52c41a', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                        >
                            + Add Section
                        </button>
                    </div>
                    <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
                        {(linkedLicence.containedIn || [])
                            .filter(s => s.direction === LinkedLicenceDirection.Outgoing)
                            .map((section, idx) => (
                            <li key={idx} style={{ marginBottom: '12px', padding: '8px', border: '1px solid #ddd', borderRadius: '4px', backgroundColor: 'white' }}>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px' }}>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.75rem' }}>Section Name:</label>
                                        <input 
                                            type="text" 
                                            value={section.sectionName || ''} 
                                            onChange={(e) => handleSectionChange(idx, 'sectionName', e.target.value)}
                                            style={{ width: '100%', padding: '2px' }}
                                        />
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.75rem' }}>Link Reason:</label>
                                        <input 
                                            type="text" 
                                            value={section.linkReason || ''} 
                                            onChange={(e) => handleSectionChange(idx, 'linkReason', e.target.value)}
                                            style={{ width: '100%', padding: '2px' }}
                                        />
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.75rem' }}>Line Number:</label>
                                        <input 
                                            type="number" 
                                            value={section.lineNumber ?? ''} 
                                            onChange={(e) => handleSectionChange(idx, 'lineNumber', parseInt(e.target.value) || 0)}
                                            style={{ width: '100%', padding: '2px' }}
                                        />
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.75rem' }}>Page Number:</label>
                                        <input 
                                            type="number" 
                                            value={section.pageNumber ?? ''} 
                                            onChange={(e) => handleSectionChange(idx, 'pageNumber', parseInt(e.target.value) || 0)}
                                            style={{ width: '100%', padding: '2px' }}
                                        />
                                    </div>
                                    <div style={{ gridColumn: 'span 2', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                        <label style={{ fontSize: '0.75rem', display: 'flex', alignItems: 'center' }}>
                                            <input 
                                                type="checkbox" 
                                                checked={!!section.isBecauseOfAggregate} 
                                                onChange={(e) => handleSectionChange(idx, 'isBecauseOfAggregate', e.target.checked)}
                                                style={{ marginRight: '4px' }}
                                            />
                                            Because of Aggregate
                                        </label>
                                        <button 
                                            onClick={() => handleRemoveSection(idx)}
                                            style={{ padding: '2px 6px', fontSize: '0.7rem', backgroundColor: '#ff7875', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                                        >
                                            Remove Section
                                        </button>
                                    </div>
                                </div>
                            </li>
                        ))}
                    </ul>
                </div>
            </div>
        );
    }

    return (
        <div className="linked-licence-item" style={{ padding: '8px', borderBottom: '1px solid #eee' }}>
            <p style={{ margin: '0 0 4px 0' }}><strong>Linked Licence Number:</strong> {linkedLicence.licenceNumber || 'N/A'}</p>
            <p style={{ margin: '0 0 4px 0' }}><strong>Permit Number:</strong> {linkedLicence.permitNumber || 'N/A'}</p>
            {linkedLicence.containedIn && linkedLicence.containedIn.filter(s => s.direction === LinkedLicenceDirection.Outgoing).length > 0 && (
                <div style={{ marginTop: '8px', fontSize: '0.9rem' }}>
                    <strong>Contained In:</strong>
                    <ul style={{ margin: '4px 0 0 0', paddingLeft: '20px' }}>
                        {linkedLicence.containedIn
                            .filter(s => s.direction === LinkedLicenceDirection.Outgoing)
                            .map((section, idx) => (
                            <li key={idx} style={{ marginBottom: '4px' }}>
                                <div><strong>Section:</strong> {section.sectionName || 'N/A'}</div>
                                <div><strong>Link Reason:</strong> {section.linkReason || 'N/A'}</div>
                                <div><strong>Because of Aggregate:</strong> {section.isBecauseOfAggregate ? 'Yes' : 'No'}</div>
                                <div><strong>Line:</strong> {section.lineNumber ?? 'N/A'}, <strong>Page:</strong> {section.pageNumber ?? 'N/A'}</div>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </div>
    );
};
