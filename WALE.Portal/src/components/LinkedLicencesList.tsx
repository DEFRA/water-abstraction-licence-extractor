import type {OutputListDataItem} from "../api/generated/apiClient.ts";
import LinkedLicencesListItem from "./LinkedLicencesListItem";
import {compareAlphanumeric} from "../utils/formatting.ts";
import {groupBySuccession} from "../utils/linkedLicenceSuccession.ts";

interface LinkedLicencesListProps {
    item: OutputListDataItem;
    onOpenReport: (fileId: string, licenceId: number, matchesResultId: number) => void;
}

export function LinkedLicencesList({item, onOpenReport}: LinkedLicencesListProps) {
    if (!item.linkedLicences?.length) {
        return 'No Linked Licences';
    }

    const grouped = groupBySuccession(item.linkedLicences)
        .sort((a, b) => compareAlphanumeric(a.current.licenceNumber, b.current.licenceNumber));

    return (<ul>
        {grouped.map((group, index) => (
            <LinkedLicencesListItem
                key={index}
                linkedLicence={group.current}
                superseded={group.superseded}
                onOpenReport={onOpenReport}
            />
        ))}
    </ul>);
}

export default LinkedLicencesList;