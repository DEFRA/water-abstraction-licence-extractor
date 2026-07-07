import type { ProcessRunQuery } from "../class/ProcessRunQuery.tsx";
import { OutputListDataItem } from "../api/generated/apiClient";
import { useMemo } from "react";

type ProcessRunFiltersProps = {
    data: OutputListDataItem[];
    query: ProcessRunQuery;
    updateQuery: <K extends keyof ProcessRunQuery>(
        key: K,
        value: ProcessRunQuery[K]
    ) => void;
};

export function ProcessRunFilters({
                                      data,
                                      query,
                                      updateQuery
                                  }: ProcessRunFiltersProps) {

    const uniqueIssuers = useMemo(() => {
        const issuers = new Set(
            data
                .map(item => item.issuer)
                .filter((issuer): issuer is string => Boolean(issuer))
        );

        return Array.from(issuers).sort();
    }, [data]);

    // Generate unique years for dropdown
    const uniqueYears = useMemo(() => {
        const years = new Set(
            data
                .map(item => item.issueDate?.split('-')[0])
                .filter(year => year && parseInt(year) >= 1900)
        );
        return Array.from(years).sort().reverse();
    }, [data]);

    // Generate unique license sets for dropdown
    const uniqueLicenseSets = useMemo(() => {
        const sets = new Set<string>();
        data.forEach(item => {
            item.licenceSets?.slice(1).forEach(ls => {
                if (ls.shortLicenceSetId) {
                    sets.add(ls.shortLicenceSetId);
                }
            });
        });
        let results= Array.from(sets).sort();
        return results;
    }, [data]);

    return (
        <tr>
            <td></td>
            <td></td>
            <td>
                <select
                    value={
                        query.purposesEmpty === undefined
                            ? ""
                            : String(query.purposesEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "purposesEmpty",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }
                >
                    <option value="">Any</option>
                    <option value="false">Not empty</option>
                    <option value="true">Empty</option>
                </select>
            </td>
            <td>
                <select
                    value={
                        query.pointsEmpty === undefined
                            ? ""
                            : String(query.pointsEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "pointsEmpty",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }
                >
                    <option value="">Any</option>
                    <option value="false">Not empty</option>
                    <option value="true">Empty</option>
                </select>
            </td>
            <td>
                <select
                    value={
                        query.limitsEmpty === undefined
                            ? ""
                            : String(query.limitsEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "limitsEmpty",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }
                >
                    <option value="">Any</option>
                    <option value="false">Not empty</option>
                    <option value="true">Empty</option>
                </select>
            </td>
            <td>
                <select
                    value={
                        query.aggregatesEmpty === undefined
                            ? ""
                            : String(query.aggregatesEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "aggregatesEmpty",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }
                >
                    <option value="">Any</option>
                    <option value="false">Not empty</option>
                    <option value="true">Empty</option>
                </select>
            </td>
            <td>
                <select
                    value={
                        query.ocrScan === undefined
                            ? ""
                            : String(query.purposesEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "ocrScan",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }

                >
                    <option value="">All scan</option>
                    <option value="true">True</option>
                    <option value="false">False</option>
                </select>
            </td>
            <td>
                <select
                    value={ query.issueYear === undefined
                        ? ""
                        : String(query.issueYear)}
                    onChange={e =>
                        updateQuery(
                            "issueYear",
                            e.target.value === "" ? undefined : Number(e.target.value)
                        )
                    }
                >
                    <option value="">All years</option>

                    {uniqueYears.map(year => (
                        <option key={year} value={year}>
                            {year}
                        </option>
                    ))}
                </select>
            </td>
            <td>
                <select
                    value={query.issuer ?? ""}
                    onChange={e =>
                        updateQuery(
                            "issuer",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All issuers</option>

                    {uniqueIssuers.map(issuer => (
                        <option key={issuer} value={issuer}>
                            {issuer}
                        </option>
                    ))}
                </select>
            </td>

            <td>
                <select
                    value={
                        query.meansFound === undefined
                            ? ""
                            : String(query.purposesEmpty)
                    }
                    onChange={e =>
                        updateQuery(
                            "meansFound",
                            e.target.value === ""
                                ? undefined
                                : e.target.value === "true"
                        )
                    }

                >
                    <option value="">All mean</option>
                    <option value="true">True</option>
                    <option value="false">False</option>
                </select>
            </td>
            

            <td>
                <select
                    value={query.linkedLicencesType ?? ""}
                    onChange={e =>
                        updateQuery(
                            "linkedLicencesType",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All</option>
                    <option value="AbstractionLimits">In Limits</option>
                    <option value="FurtherConditions">In Further Conditions</option>
                    <option value="ReasonsForConditions">In Reasons For Conditions</option>
                    <option value="Additional">In Additional</option>
                    <option value="Records">In Records</option>
                    <option value="Points">In Points</option>
                    <option value="LicenceHistory">In Licence History</option>
                    <option value="Purposes">In Purposes</option>
                    <option value="ImplicitBackLink">Implicit back link</option>
                    <option value="OtherConditions">Other Conditions</option>
                    <option value="FurtherProvisions">Further Provisions</option>
                    
                </select>
            </td>
            <td>
                <select
                    value={query.ShortLicenceSetId ?? ""}
                    onChange={e =>
                        updateQuery(
                            "ShortLicenceSetId",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All Licence Sets</option>

                    {uniqueLicenseSets.map(licenceSet => (
                        <option key={licenceSet} value={licenceSet}>
                            {licenceSet}
                        </option>
                    ))}
                </select>
            </td>
            <td>
                <select
                    value={query.verificationType ?? ""}
                    onChange={e =>
                        updateQuery(
                            "verificationType",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All</option>
                    <option value="AutoConfirm">AutoConfirm</option>
                    <option value="AutoWarn">AutoWarn</option>
                    <option value="AutoFail">AutoFail</option>
                    <option value="Confirmed">Confirmed</option>
                    <option value="Removed">Removed</option>
                    <option value="Edited">Edited</option>
                    <option value="Added">Added</option>
                  
                </select>
            </td>
        </tr>
    );
}

export default ProcessRunFilters;