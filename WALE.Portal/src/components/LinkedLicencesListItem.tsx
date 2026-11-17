import {
    LinkedLicence,
    NullableOfInformationDirection
} from "../api/generated/apiClient.ts";
import {NaldStatusTag} from "./NaldStatusTag.tsx";
import {useFileIdMap} from "../utils/useFileIdMap.tsx";
import {AggregateTag} from "./AggregateTag.tsx";
import NaldOnlyTag from "./NaldOnlyTag.tsx";
import ImpoundmentTag from "./ImpoundmentTag.tsx";
import {ReferencedAlongsideTag} from "./ReferencedAlongsideTag.tsx";
import {IncomingTag} from "./IncomingTag.tsx";
import {OutgoingTag} from "./OutgoingTag.tsx";
import {HistoryTag} from "./HistoryTag.tsx";

interface LinkedLicencesListItemProps {
    linkedLicence: LinkedLicence;
    onOpenReport: (filename: string, licenceId: number, matchesResultId: number) => void;
}

export function LinkedLicencesListItem({linkedLicence, onOpenReport}: LinkedLicencesListItemProps) {
    const {getFileId, getLicenceId, getMatchesResultId} = useFileIdMap();
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

    let linkedFileId = getFileId(licenceNumber);
    let linkedLicenceId = getLicenceId(licenceNumber);
    let linkedMatchesResultId = getMatchesResultId(licenceNumber);

    if (linkedFileId) {
        return (
            <li title={text}>
                <a style={{color}}
                   href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(linkedFileId, linkedLicenceId!, linkedMatchesResultId!);
                   }}>{styledLicenceNumber}
                </a>
                <NaldStatusTag status={linkedLicence.naldStatus}/>
                <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                <ReferencedAlongsideTag containedIn={linkedLicence.containedIn}/>
                <IncomingTag containedIn={linkedLicence.containedIn}/>
                <OutgoingTag containedIn={linkedLicence.containedIn}/>
                <AggregateTag isBecauseOfAggregate={linkedLicence.isBecauseOfAggregate}/>
                <HistoryTag containedIn={linkedLicence.containedIn}/>
                <ImpoundmentTag licenceType={linkedLicence.licenceType}/>
            </li>
        );
    } else {
        return (
            <li title={text} style={{color}}>
                {styledLicenceNumber}
                <NaldStatusTag status={linkedLicence.naldStatus}/>
                <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                <ReferencedAlongsideTag containedIn={linkedLicence.containedIn}/>
                <IncomingTag containedIn={linkedLicence.containedIn}/>
                <OutgoingTag containedIn={linkedLicence.containedIn}/>
                <AggregateTag isBecauseOfAggregate={linkedLicence.isBecauseOfAggregate}/>
                <HistoryTag containedIn={linkedLicence.containedIn}/>
                <ImpoundmentTag licenceType={linkedLicence.licenceType}/>
            </li>
        );
    }
}

export default LinkedLicencesListItem;