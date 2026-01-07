import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LicenceSetsListItem from "./LicenceSetsListItem.tsx";
import {getLicenceSetTypeClass} from "../utils/licenceSetTypeUtils.ts";

interface LicenceSetsListProps {
    item: OutputListDataItem;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
    showSingles: boolean;
}

export function LicenceSetsList({item, onOpenLicenceSetReport, showSingles}: LicenceSetsListProps) {
    if (!item.licenceSets?.length) {
        return '--';
    }
    
    let hasOnlySingles = item.licenceSets.every(ls => getLicenceSetTypeClass(ls.licenceSetType) == 'singleLicenceOnly');
    
    if (!showSingles && hasOnlySingles)
    {
        return '--';
    }
    
    return (<ul>
        {item.licenceSets.map((ls, index) => (
            <LicenceSetsListItem 
                key={index} 
                licenceSet={ls} 
                filename={item.filename}
                onOpenLicenceSetReport={onOpenLicenceSetReport}
            />
        ))}
    </ul>);
}

export default LicenceSetsList;