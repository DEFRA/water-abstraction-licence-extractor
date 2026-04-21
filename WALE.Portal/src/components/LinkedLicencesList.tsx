import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LinkedLicencesListItem from "./LinkedLicencesListItem";

interface LinkedLicencesListProps {
    item: OutputListDataItem;
    data: OutputListDataItem[];
    onOpenReport: (fileId: string) => void;
}

export function LinkedLicencesList({item, data, onOpenReport}: LinkedLicencesListProps) {
    if (!item.linkedLicences?.length) {
        return '--';
    }

    return (<ul>
        {item.linkedLicences.map((ll, index) => (
            <LinkedLicencesListItem
                key={index}
                linkedLicence={ll}
                data={data}
                onOpenReport={onOpenReport}
            />
        ))}
    </ul>);
}

export default LinkedLicencesList;