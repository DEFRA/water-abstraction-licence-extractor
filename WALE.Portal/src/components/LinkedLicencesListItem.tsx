import {Link} from "react-router-dom";
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
    linkToEstate?: boolean;
    onOpenReport: (filename: string, licenceId: number, matchesResultId: number) => void;
}

export function LinkedLicencesListItem({linkedLicence, linkToEstate, onOpenReport}: LinkedLicencesListItemProps) {
    const {getFileId, getLicenceId, getMatchesResultId} = useFileIdMap();
    let licenceNumber = linkedLicence.licenceNumber;
    let backLink = linkedLicence.containedIn?.length! > 0
        && linkedLicence.containedIn?.every(section =>
            section.direction === NullableOfInformationDirection.Incoming
                || section.direction === NullableOfInformationDirection.ReferencedAlongside);
    
    let abstractionLimits = linkedLicence.containedIn?.some(section => section.sectionName?.includes("AbstractionLimits")) ?? false;

    let styledLicenceNumber = backLink && false ? ("(" + linkedLicence.licenceNumber + ")") : linkedLicence.licenceNumber;

    let text = backLink
        ? 'Incoming/refenced alongside link'
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
    } else if (linkToEstate && licenceNumber) {
        /// No file id means the run did not extract the licence, which is every linked licence
        /// on the estate page - it mounts no FileIdMapProvider at all, having no single run to
        /// key one on. The estate holds every run, so the lookup answers where the modal cannot.
        return (
            <li title={text}>
                <Link style={{color}}
                      to={`/licences?licenceNumber=${encodeURIComponent(licenceNumber)}`}>
                    {styledLicenceNumber}
                </Link>
                <NaldStatusTag status={linkedLicence.naldStatus}/>
                <NaldOnlyTag containedIn={linkedLicence.containedIn}/>
                <AggregateTag isBecauseOfAggregate={linkedLicence.isBecauseOfAggregate}/>
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