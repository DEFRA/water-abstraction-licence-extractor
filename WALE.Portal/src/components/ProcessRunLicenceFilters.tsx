import { useEffect, useState } from "react";
import type { ProcessRunQuery } from "../class/ProcessRunQuery.tsx";

type ProcessRunLicenceFiltersProps = {
    issuers?: string[];
    shortLicenceIds?: string[];
    issueDates?: string[];
    query: ProcessRunQuery;
    setQuery: React.Dispatch<React.SetStateAction<ProcessRunQuery>>;
    setPageNumber: (pageNumber: number) => void;
};

export function ProcessRunLicenceFilters({
                                             query,
                                             setQuery,
                                             issuers,
                                             shortLicenceIds,
                                             issueDates,
                                             setPageNumber
                                         }: ProcessRunLicenceFiltersProps) {
    const [pendingQuery, setPendingQuery] = useState<ProcessRunQuery>(query);

    useEffect(() => {
        setPendingQuery(query);
    }, [query]);

    const updatePendingQuery = <K extends keyof ProcessRunQuery>(
        key: K,
        value: ProcessRunQuery[K]
    ) => {
        setPendingQuery(prev => ({
            ...prev,
            [key]: value
        }));
    };

    const applyQuery = (queryToApply: ProcessRunQuery) => {
        setPageNumber(1);
        setQuery(prev => ({
            ...prev,
            ...queryToApply,
            skip: 0
        }));
    };

    const applyFilters = () => {
        applyQuery(pendingQuery);
    };

    const clearFilters = () => {
        const clearedQuery: ProcessRunQuery = {
            ...pendingQuery,
            purposesEmpty: undefined,
            pointsEmpty: undefined,
            limitsEmpty: undefined,
            aggregatesEmpty: undefined,
            ocrScan: undefined,
            issueYear: undefined,
            issuer: undefined,
            meansFound: undefined,
            linkedLicencesType: undefined,
            ShortLicenceSetId: undefined,
            verificationType: undefined
        };

        setPendingQuery(clearedQuery);
        applyQuery(clearedQuery);
    };

    return (
        <tr>
            <td> 
                <button
                type="button"
                className="clear-filters-button"
                onClick={clearFilters}
            >
                Clear Filters
            </button>
            </td>

            <td>
               

                <button
                    type="button"
                    className="apply-filters-button"
                    onClick={applyFilters}
                    style={{ marginLeft: "8px" }}
                >
                    Apply Filters
                </button>
            </td>

            <td>
                <select
                    className={pendingQuery.purposesEmpty === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.purposesEmpty === undefined
                            ? ""
                            : String(pendingQuery.purposesEmpty)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={pendingQuery.pointsEmpty === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.pointsEmpty === undefined
                            ? ""
                            : String(pendingQuery.pointsEmpty)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={pendingQuery.limitsEmpty === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.limitsEmpty === undefined
                            ? ""
                            : String(pendingQuery.limitsEmpty)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={pendingQuery.aggregatesEmpty === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.aggregatesEmpty === undefined
                            ? ""
                            : String(pendingQuery.aggregatesEmpty)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={pendingQuery.ocrScan === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.ocrScan === undefined
                            ? ""
                            : String(pendingQuery.ocrScan)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={!pendingQuery.issueYear ? "" : "filter-active"}
                    value={
                        pendingQuery.issueYear === undefined
                            ? ""
                            : String(pendingQuery.issueYear)
                    }
                    onChange={e =>
                        updatePendingQuery(
                            "issueYear",
                            e.target.value === "" ? undefined : Number(e.target.value)
                        )
                    }
                >
                    <option value="">All years</option>

                    {issueDates?.map(year => (
                        <option key={year} value={year}>
                            {year}
                        </option>
                    ))}
                </select>
            </td>

            <td>
                <select
                    className={!pendingQuery.issuer ? "" : "filter-active"}
                    value={pendingQuery.issuer ?? ""}
                    onChange={e =>
                        updatePendingQuery(
                            "issuer",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All issuers</option>

                    {issuers?.map(issuer => (
                        <option key={issuer} value={issuer}>
                            {issuer}
                        </option>
                    ))}
                </select>
            </td>

            <td>
                <select
                    className={pendingQuery.meansFound === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.meansFound === undefined
                            ? ""
                            : String(pendingQuery.meansFound)
                    }
                    onChange={e =>
                        updatePendingQuery(
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
                    className={!pendingQuery.linkedLicencesType ? "" : "filter-active"}
                    value={pendingQuery.linkedLicencesType ?? ""}
                    onChange={e =>
                        updatePendingQuery(
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
                    className={!pendingQuery.ShortLicenceSetId ? "" : "filter-active"}
                    value={pendingQuery.ShortLicenceSetId ?? ""}
                    onChange={e =>
                        updatePendingQuery(
                            "ShortLicenceSetId",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All Licence Sets</option>

                    {shortLicenceIds?.map(licenceSet => (
                        <option key={licenceSet} value={licenceSet}>
                            {licenceSet}
                        </option>
                    ))}
                </select>
            </td>

            <td>
                <select
                    className={!pendingQuery.verificationType ? "" : "filter-active"}
                    value={pendingQuery.verificationType ?? ""}
                    onChange={e =>
                        updatePendingQuery(
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

export default ProcessRunLicenceFilters;