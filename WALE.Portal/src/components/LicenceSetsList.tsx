import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LicenceSetsListItem from "./LicenceSetsListItem.tsx";

interface LicenceSetsListProps {
    item: OutputListDataItem;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
}

export function LicenceSetsList({item, onOpenLicenceSetReport}: LicenceSetsListProps) {
    if (!item.licenceSets?.length) {
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