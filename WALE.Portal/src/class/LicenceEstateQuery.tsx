import type { ProcessRunQuery } from "./ProcessRunQuery.tsx";

export type LicenceEstateQuery = ProcessRunQuery & {
    queue?: number;
    projectedStatus?: string;
    processRunId?: number;
};
