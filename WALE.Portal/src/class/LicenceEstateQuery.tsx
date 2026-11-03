import type { ProcessRunQuery } from "./ProcessRunQuery.tsx";

export type LicenceEstateQuery = ProcessRunQuery & {
    queue?: number;
    minimumDocumentCount?: number;
    projectedStatus?: string;
    processRunId?: number;
};
