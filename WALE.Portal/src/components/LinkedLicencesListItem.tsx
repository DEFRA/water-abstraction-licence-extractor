import {
    LinkedLicence,
    InformationDirection,
    OutputListDataItem
} from "../api/generated/apiClient.ts";
import {NaldStatusTag} from "./NaldStatusTag.tsx";
import {getFileId} from "../utils/verificationUtils.ts";

interface LinkedLicencesListItemProps {
    linkedLicence: LinkedLicence;
    data: OutputListDataItem[];
    onOpenReport: (filename: string) => void;
}

export function LinkedLicencesListItem({linkedLicence, data, onOpenReport}: LinkedLicencesListItemProps) {
    let licenceNumber = linkedLicence.licenceNumber;
    let backLink = linkedLicence.containedIn?.length! > 0 && linkedLicence.containedIn?.every(section => section.direction === InformationDirection.Incoming);
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

export default LinkedLicencesListItem;