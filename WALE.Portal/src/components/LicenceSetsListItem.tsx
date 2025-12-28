import type {LicenceSet} from "../api/generated/apiClient.ts";

interface LicenceSetsListItemProps {
    filename: string | undefined;
    licenceSet: LicenceSet;
}

export function LicenceSetsListItem({filename, licenceSet}: LicenceSetsListItemProps) {
    let licenceSetId = licenceSet.licenceSetId;
    let shortLicenceSetId = licenceSet.shortLicenceSetId;

    let backLink = licenceSet.licenceSetType === "allLicencesImplicitlyReferencedInLimits";
    let abstractionLimits = licenceSet.licenceSetType === "allLicencesExplicitlyReferencedInLimits";
    let mixed = licenceSet.licenceSetType === "allLicencesIncludingImplicitlyReferenced";
    let fullyEncompassedIn = licenceSet.licenceSetType === "fullyEncompassedIn";
    let partiallyEncompassedIn = licenceSet.licenceSetType === "partiallyEncompassedIn";

    let color = backLink ? "#AAA" : "black";

    if (abstractionLimits) {
        color = "lightseagreen";
    }

    if (mixed) {
        color = "orange";
    }

    if (fullyEncompassedIn) {
        color = "forestgreen"
    }

    if (partiallyEncompassedIn) {
        color = "hotpink"
    }

    return (
        <li className={licenceSet.licenceSetType}>
            <span className='lsId' title={licenceSetId + " " + licenceSet.licenceSetType}>
                <a
                    style={{color}}
                    //href={getLicenceSetReportUrl(filename, licenceSetId)}
                    href={filename}
                    onClick={(e) => {
                        e.preventDefault();
                        // openIframeSet(filename, licenceSetId);
                    }}>
                    {shortLicenceSetId}
                </a>
            </span>
        </li>);
}

export default LicenceSetsListItem;