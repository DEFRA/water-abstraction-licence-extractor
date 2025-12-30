import type {LicenceSet} from "../api/generated/apiClient.ts";

interface LicenceSetsListItemProps {
    filename: string | undefined;
    licenceSet: LicenceSet;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
}

export function LicenceSetsListItem({filename, licenceSet, onOpenLicenceSetReport}: LicenceSetsListItemProps) {
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