import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LinkedLicencesListItem from "./LinkedLicencesListItem.tsx";

interface LinkedLicencesListProps {
    item: OutputListDataItem;
    data: OutputListDataItem[];
}

export function LinkedLicencesList({item, data}: LinkedLicencesListProps) {
    if (!item.linkedLicences?.length) {
        return '--';
    }
    
    return (<ul>
        {item.linkedLicences.map((ll, index) => (
            <LinkedLicencesListItem key={index} linkedLicence={ll} data={data} />
        ))}
    </ul>);
}

export default LinkedLicencesList;