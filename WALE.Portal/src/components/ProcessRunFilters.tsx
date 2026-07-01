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
                    value={query.issuer ?? ""}
                    onChange={e =>
                        updateQuery(
                            "issuer",
                            e.target.value === "" ? undefined : e.target.value
                        )
                    }
                >
                    <option value="">All issue date</option>

                    {uniqueIssuers.map(issuer => (
                        <option key={issuer} value={issuer}>
                            {issuer}
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
                    <option value="">All</option>

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
        </tr>
    );
}

export default ProcessRunFilters;