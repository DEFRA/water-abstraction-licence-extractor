import {
    LinkedLicence,
    NullableOfInformationDirection
} from "../api/generated/apiClient.ts";
import {NaldStatusTag} from "./NaldStatusTag.tsx";
import {useFileIdMap} from "../utils/useFileIdMap.tsx";
import {AggregateTag} from "./AggregateTag.tsx";
import NaldOnlyTag from "./NaldOnlyTag.tsx";

interface LinkedLicencesListItemProps {
    linkedLicence: LinkedLicence;
    onOpenReport: (filename: string) => void;
}

export function LinkedLicencesListItem({linkedLicence, onOpenReport}: LinkedLicencesListItemProps) {
    const {getFileId} = useFileIdMap();
    let licenceNumber = linkedLicence.licenceNumber;
    let backLink = linkedLicence.containedIn?.length! > 0 && linkedLicence.containedIn?.every(section => section.direction === NullableOfInformationDirection.Incoming);
    let abstractionLimits = linkedLicence.containedIn?.some(section => section.sectionName?.includes("AbstractionLimits")) ?? false;

    let styledLicenceNumber = backLink && false ? ("(" + linkedLicence.licenceNumber + ")") : linkedLicence.licenceNumber;

    let text = backLink
        ? 'Implicit back link'
        : linkedLicence.containedIn!.length ?
            linkedLicence.containedIn![0].sectionName
            : "?";

    let color = backLink
        ? "#888"
        : abstractionLimits
            ? "lightseagreen"
            : "black";

    let linkedFilename = getFileId(licenceNumber);

    if (linkedFilename) {
        return (
            <li title={text}>
                <a style={{color}}
                   href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(linkedFilename);
                   }}>{styledLicenceNumber}
                </a>
                <NaldStatusTag status={linkedLicence.naldStatus}/>
                <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                <AggregateTag isBecauseOfAggregate={linkedLicence.isBecauseOfAggregate}/>
            </li>
        );
    } else {
        return (
            <li title={text} style={{color}}>
                {styledLicenceNumber}
                <NaldStatusTag status={linkedLicence.naldStatus}/>
                <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                <AggregateTag isBecauseOfAggregate={linkedLicence.isBecauseOfAggregate}/>
            </li>
        );
    }
}

export default LinkedLicencesListItem;