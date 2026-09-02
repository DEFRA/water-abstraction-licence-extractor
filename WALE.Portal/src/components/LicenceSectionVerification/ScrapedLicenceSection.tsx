import {type ReactElement, cloneElement} from 'react';
import { OutputListDataItem } from '../../api/generated/apiClient';
import { CollapsibleItem } from './CollapsibleItem';

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
    return (
        <CollapsibleItem
            variant="section"
            defaultOpen={initialOpen}
            summary={<h3 style={{ margin: 0, fontSize: '1.1rem' }}>{title}</h3>}
        >
            {cloneElement(children, {
                outputListDataItem: outputListDataItem,
                onOpenReport: onOpenReport,
                onItemVerificationRequested: undefined,
                scrapedView: true
            } as any)}
        </CollapsibleItem>
    );
}
