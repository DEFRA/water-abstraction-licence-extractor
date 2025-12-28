import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LicenceSetsListItem from "./LicenceSetsListItem.tsx";

interface LicenceSetsListProps {
    item: OutputListDataItem;
}

export function LicenceSetsList({item}: LicenceSetsListProps) {
    if (!item.licenceSets?.length) {
        return '--';
    }
    
    return (<ul>
        {item.licenceSets.map((ls, index) => (
            <LicenceSetsListItem key={index} licenceSet={ls} filename={item.filename}/>
        ))}
    </ul>);
}

export default LicenceSetsList;