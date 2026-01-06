import type {LicenceSet} from "../api/generated/apiClient.ts";
import {getLicenceSetTypeClass} from "../utils/licenceSetTypeUtils.ts";

interface LicenceSetsListItemProps {
    filename: string | undefined;
    licenceSet: LicenceSet;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
}

export function LicenceSetsListItem({filename, licenceSet, onOpenLicenceSetReport}: LicenceSetsListItemProps) {
    let licenceSetId = licenceSet.licenceSetId;
    let shortLicenceSetId = licenceSet.shortLicenceSetId;

    const licenceSetType = getLicenceSetTypeClass(licenceSet.licenceSetType);

    let backLink = licenceSetType === "allLicencesImplicitlyReferencedInLimits";
    let abstractionLimits = licenceSetType === "allLicencesExplicitlyReferencedInLimits";
    let mixed = licenceSetType === "allLicencesIncludingImplicitlyReferenced";
    let fullyEncompassedIn = licenceSetType === "fullyEncompassedIn";
    let partiallyEncompassedIn = licenceSetType === "partiallyEncompassedIn";

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
        <li className={licenceSetType}>
            <span className='lsId' title={licenceSetId + " " + licenceSetType}>
                <a
                    style={{color}}
                    href="#"
                    onClick={(e) => {
                        e.preventDefault();
                        onOpenLicenceSetReport(filename!, licenceSetId!);
                    }}>
                    {shortLicenceSetId}
                </a>
            </span>
        </li>);
}

export default LicenceSetsListItem;