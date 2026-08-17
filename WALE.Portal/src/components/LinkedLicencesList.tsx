import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LinkedLicencesListItem from "./LinkedLicencesListItem";

interface LinkedLicencesListProps {
    item: OutputListDataItem;
    onOpenReport: (fileId: string) => void;
}

export function LinkedLicencesList({item, onOpenReport}: LinkedLicencesListProps) {
    if (!item.linkedLicences?.length) {
        return 'No Linked Licences';
    }

    return (<ul>
        {item.linkedLicences.map((ll, index) => (
            <LinkedLicencesListItem
                key={index}
                linkedLicence={ll}
                onOpenReport={onOpenReport}
            />
        ))}
    </ul>);
}

export default LinkedLicencesList;