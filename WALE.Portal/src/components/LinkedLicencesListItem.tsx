import {
    LinkedLicence,
    NullableOfInformationDirection,
    OutputListDataItem
} from "../api/generated/apiClient.ts";
import {NaldStatusTag} from "./NaldStatusTag.tsx";

interface LinkedLicencesListItemProps {
    linkedLicence: LinkedLicence;
    data: OutputListDataItem[];
    onOpenReport: (filename: string) => void;
}

export function LinkedLicencesListItem({linkedLicence, data, onOpenReport}: LinkedLicencesListItemProps) {
    let licenceNumber = linkedLicence.licenceNumber;
    let backLink = linkedLicence.containedIn?.length! > 0 && linkedLicence.containedIn?.every(section => section.direction === NullableOfInformationDirection.Incoming);
    let abstractionLimits = linkedLicence.containedIn?.some(section => section.sectionName?.includes("AbstractionLimits")) ?? false;

    let styledLicenceNumber = backLink && false ? ("(" + linkedLicence.licenceNumber + ")") : linkedLicence.licenceNumber;
    let text = backLink
        ? 'Implicit back link'
        : linkedLicence.containedIn!.length ?
            linkedLicence.containedIn![0].sectionName
            : "?";
    let color = backLink ? "#888" : "black";

    if (abstractionLimits) {
        color = "lightseagreen";
    }

    let linkedFilename = getFileId(data, licenceNumber);

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
                <NaldStatusTag status={linkedLicence.naldStatus} />
            </li>
        );
    } else {
        return (
            <li title={text} style={{color}}>
                {styledLicenceNumber}
                <NaldStatusTag status={linkedLicence.naldStatus} />
            </li>
        );
    }
}

function getFileId(data: OutputListDataItem[], licenceNumber: string | undefined) {
    if (licenceNumber == undefined) {
        return false;
    }

    for (let itemNumber in data) {
        let item = data[itemNumber];

        if (item.licenceNumber === licenceNumber) {
            return item.fileId;
        }
    }

    return false;
}

export default LinkedLicencesListItem;