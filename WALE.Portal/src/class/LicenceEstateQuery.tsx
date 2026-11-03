import type { ProcessRunQuery } from "./ProcessRunQuery.tsx";

export type LicenceEstateQuery = ProcessRunQuery & {
    queue?: number;
    minimumDocumentCount?: number;
    maximumRunCount?: number;
    projectedStatus?: string;
    processRunId?: number;
};
