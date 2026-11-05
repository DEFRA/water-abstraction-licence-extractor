import type {OutputListDataItemLicenceSet} from "../api/generated/apiClient.ts";
import {getLicenceSetTypeClass} from "../utils/licenceSetTypeUtils.ts";

interface LicenceSetsListItemProps {
    fileId: string | undefined;
    licenceId: number | undefined;
    matchesResultId: number | undefined;
    licenceSet: OutputListDataItemLicenceSet;
    onOpenLicenceSetReport: (fileId: string, licenceId: number, matchesResultId: number, licenceSetId: string) => void;
}

export function LicenceSetsListItem({fileId, licenceId, matchesResultId, licenceSet, onOpenLicenceSetReport}: LicenceSetsListItemProps) {
    let licenceSetId = licenceSet.licenceSetId;
    let shortLicenceSetId = licenceSet.shortLicenceSetId;

    const licenceSetType = getLicenceSetTypeClass(licenceSet.licenceSetType);

    let groupedByAbstractionLimits = licenceSetType === "licencesGroupedByAbstractionLimits";
    let singleLicenceOnly = licenceSetType === "singleLicenceOnly";
    let others = licenceSetType === "others";
    let unknown = licenceSetType === "unknown";

    let color = groupedByAbstractionLimits ? "hotpink" : "red";

    if (singleLicenceOnly) {
        color = "forestgreen";
    }

    if (others) {
        color = "black"
    }

    if (unknown) {
        color = "orange"
    }

    return (
        <li className={licenceSetType}>
            <span className='lsId' title={licenceSetId + " " + licenceSetType}>
                <a
                    style={{color}}
                    href="#"
                    onClick={(e) => {
                        e.preventDefault();
                        onOpenLicenceSetReport(fileId!, licenceId!, matchesResultId!, licenceSetId!);
                    }}>
                    {shortLicenceSetId}
                </a>
            </span>
        </li>);
}

export default LicenceSetsListItem;