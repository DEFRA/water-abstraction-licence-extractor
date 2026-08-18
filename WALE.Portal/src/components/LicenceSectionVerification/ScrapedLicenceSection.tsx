import {type ReactElement, useState, cloneElement} from 'react';
import { OutputListDataItem } from '../../api/generated/apiClient';

export interface LicenceSectionBodyProps {
    outputListDataItem?: OutputListDataItem;
    onOpenReport?: (fileId: string) => void;
}

interface ScrapedLicenceSectionProps {
    title: string;
    itemType?: string;
    children: ReactElement<LicenceSectionBodyProps>;
    initialOpen?: boolean;
    licenceFileId: string;
    processRunId: number;
    onRefresh?: () => void;
    outputListDataItem?: OutputListDataItem;
    onOpenReport?: (fileId: string) => void;
}

export function ScrapedLicenceSection({ title, children, initialOpen = false, outputListDataItem, onOpenReport }: ScrapedLicenceSectionProps) {
    const [isOpen, setIsOpen] = useState(initialOpen);

    return (
        <div className="licence-section informational-only" style={{ border: '1px solid #ccc', marginBottom: '10px', borderRadius: '4px' }}>
            <div 
                className="licence-section-header" 
                style={{ 
                    padding: '10px', 
                    backgroundColor: '#f5f5f5', 
                    cursor: 'pointer', 
                    display: 'flex', 
                    justifyContent: 'space-between',
                    alignItems: 'center'
                }}
                onClick={() => setIsOpen(!isOpen)}
            >
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>{title}</h3>
                <span style={{ marginLeft: '10px' }}>{isOpen ? '▲' : '▼'}</span>
            </div>
            {isOpen && (
                <div className="licence-section-body" style={{ padding: '10px', borderTop: '1px solid #ccc' }}>
                    {cloneElement(children, {
                        outputListDataItem: outputListDataItem,
                        onOpenReport: onOpenReport,
                        onItemVerificationRequested: undefined,
                        scrapedView: true
                    } as any)}
                </div>
            )}
        </div>
    );
}
