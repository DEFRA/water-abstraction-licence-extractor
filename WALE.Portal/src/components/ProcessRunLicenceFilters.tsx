import { useEffect, useState } from "react";
import type { ProcessRunQuery } from "../class/ProcessRunQuery.tsx";
import type { OutputListDataItemKey} from "../utils/types.ts";

type ProcessRunLicenceFiltersProps = {
    issuers?: string[];
    shortLicenceIds?: string[];
    issueDates?: string[];
    query: ProcessRunQuery;
    setQuery: React.Dispatch<React.SetStateAction<ProcessRunQuery>>;
    setPageNumber: (pageNumber: number) => void;
    showSingles: boolean;
    onToggleSingles: (checked: boolean) => void;
};

export function ProcessRunLicenceFilters({
                                             query,
                                             setQuery,
                                             issuers,
                                             shortLicenceIds,
                                             issueDates,
                                             setPageNumber,
                                             onToggleSingles,
                                             showSingles
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
            aggregatesFilter: undefined,
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
    const handleSort = (field: OutputListDataItemKey) => {
        const nextAscending =
            query.sortField === field
                ? !(query.sortAscending ?? true)
                : true;

        const sortedQuery: ProcessRunQuery = {
            ...pendingQuery,
            sortField: field,
            sortAscending: nextAscending
        };

        setPendingQuery(sortedQuery);
        applyQuery(sortedQuery);
    };

    return (
        <>
            <tr>
                <td colSpan={11}></td>
                <td>
                    <input
                        type="checkbox"
                        id="showSingles"
                        checked={showSingles}
                        onChange={(e) => onToggleSingles(e.target.checked)}
                    /> Show singles
                </td>
                <td colSpan={2}></td>
            </tr>
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
                    className={pendingQuery.aggregatesFilter === undefined ? "" : "filter-active"}
                    value={
                        pendingQuery.aggregatesFilter === undefined
                            ? ""
                            : String(pendingQuery.aggregatesFilter)
                    }
                    onChange={e =>
                        updatePendingQuery(
                            "aggregatesFilter",
                            e.target.value
                        )
                    }
                >
                    <option value="">Any</option>
                    <option value="null_false">File: Any, Nald: False</option>
                    <option value="null_true">File: Any, Nald: True</option>
                    <option value="false_null">File: No, Nald: Any</option>
                    <option value="false_false">File: No, Nald: No</option>
                    <option value="false_true">File: No, Nald: Yes</option>
                    <option value="true_null">File: Yes, Nald: Any</option>
                    <option value="true_true">File: Yes, Nald: Yes</option>
                    <option value="true_false">File: Yes, Nald: No</option>
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
                    <option value="NoRecords">No Records</option>
                </select>
            </td>

            <td className='default-hidden'>
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
                    <option value="RequestBusinessReview">Request Business Review</option>
                    <option value="CompleteBusinessReview">Complete Business Review</option>
                    <option value="NoVerification">No Verification</option>
                </select>
            </td>
        </tr>
    <tr>
        <td style={{width: '5%'}}>&nbsp;</td>
        <td style={{width: '7%'}}>
            Licence no. <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceNumber'); }}>&#8693;</a>
        </td>
        <td style={{width: '5%'}} className="default-hidden">
            Licence holder <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceHolder'); }}>&#8693;</a>
        </td>
        <td style={{width: '14%'}}>
            Purposes <a href="#" onClick={(e) => { e.preventDefault(); handleSort('purposes'); }}>&#8693;</a>
        </td>
        <td style={{width: '14%'}}>
            Points <a href="#" onClick={(e) => { e.preventDefault(); handleSort('points'); }}>&#8693;</a>
        </td>
        <td style={{width: '7%'}}>
            Abs. limits <a href="#" onClick={(e) => { e.preventDefault(); handleSort('limitsCount'); }}>&#8693;</a>
        </td>
        <td style={{width: '5%'}}>
            Agg's <a href="#" onClick={(e) => { e.preventDefault(); handleSort('aggregatesCount'); }}>&#8693;</a>
        </td>
        <td style={{width: '5%'}}>
            Scan <a href="#" onClick={(e) => { e.preventDefault(); handleSort('ocr'); }}>&#8693;</a>
        </td>
        <td style={{width: '5%'}}>
            Iss. date <a href="#" onClick={(e) => { e.preventDefault(); handleSort('issueDate'); }}>&#8693;</a>
        </td>
        <td style={{width: '6%'}}>
            Issuer <a href="#" onClick={(e) => { e.preventDefault(); handleSort('issuer'); }}>&#8693;</a>
        </td>
        <td style={{width: '7%'}}>
            Means of abs. <a href="#" onClick={(e) => { e.preventDefault(); handleSort('meansFound'); }}>&#8693;</a>
        </td>
        <td style={{width: '7%'}}>
            Linked&nbsp;licences <a href="#" onClick={(e) => { e.preventDefault(); handleSort('linkedLicences'); }}>&#8693;</a>
        </td>
        <td className='default-hidden' style={{width: '8%'}}>
            Licence sets <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceSets'); }}>&#8693;</a>
        </td>
        <td style={{width: '10%'}}>
            Verified <a href="#" onClick={(e) => { e.preventDefault(); handleSort('latestLicenceSectionVerifications'); }}>&#8693;</a>
        </td>
    </tr>
        </>
    );
}

export default ProcessRunLicenceFilters;