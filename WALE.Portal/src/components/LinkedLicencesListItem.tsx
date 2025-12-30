import type {LinkedLicence, OutputListDataItem} from "../api/generated/apiClient.ts";

interface LinkedLicencesListItemProps {
    linkedLicence: LinkedLicence;
    data: OutputListDataItem[];
    onOpenReport: (filename: string) => void;
}

export function LinkedLicencesListItem({linkedLicence, data, onOpenReport}: LinkedLicencesListItemProps) {
    let licenceNumber = linkedLicence.licenceNumber;
    let backLink = linkedLicence.fromSection.length === 1 && linkedLicence.fromSection[0].indexOf("ImplicitBackLink") > -1;
    let abstractionLimits = linkedLicence.fromSection.length >= 1 && linkedLicence.fromSection.indexOf("AbstractionLimits") > -1;

    let styledLicenceNumber = backLink && false ? ("(" + linkedLicence.licenceNumber + ")") : linkedLicence.licenceNumber;
    let text = backLink ? 'Implicit back link' : linkedLicence.fromSection[0];
    let color = backLink ? "#888" : "black";

    if (abstractionLimits) {
        color = "lightseagreen";
    }

    let linkedFilename = getFilename(data, licenceNumber);

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
            </li>
        );
    } else {
        return (
            <li title={text} style={{color}}>
                {styledLicenceNumber}
            </li>
        );
    }
}

function getFilename(data: OutputListDataItem[], licenceNumber: string | undefined) {
    if (licenceNumber == undefined) {
        return false;
    }

    for (let itemNumber in data) {
        let item = data[itemNumber];

        if (item.licenceNumber === licenceNumber) {
            return item.filename;
        }
    }

    return false;
}

export default LinkedLicencesListItem;