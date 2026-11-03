import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LinkedLicencesListItem from "./LinkedLicencesListItem";
import {compareAlphanumeric} from "../utils/formatting.ts";

interface LinkedLicencesListProps {
    item: OutputListDataItem;
    linkToEstate?: boolean;
    onOpenReport: (fileId: string, licenceId: number, matchesResultId: number) => void;
}

export function LinkedLicencesList({item, linkToEstate, onOpenReport}: LinkedLicencesListProps) {
    if (!item.linkedLicences?.length) {
        return 'No Linked Licences';
    }

    const sorted = [...item.linkedLicences].sort((a, b) => compareAlphanumeric(a.licenceNumber, b.licenceNumber));

    return (<ul>
        {sorted.map((ll, index) => (
            <LinkedLicencesListItem
                key={index}
                linkedLicence={ll}
                linkToEstate={linkToEstate}
                onOpenReport={onOpenReport}
            />
        ))}
    </ul>);
}

export default LinkedLicencesList;