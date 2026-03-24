import type {OutputListDataItem} from "../api/generated/apiClient.ts";

export type FilterType =
    | 'Year'
    | 'Bool'
    | 'EmptyOrNot'
    | 'EmptyOrNotInt'
    | 'EmptyOrNotArray'
    | 'ArrayValue'
    | 'ArrayValueMapped'
    | 'ArrayValueArrayValueMapped'
    | 'Values';

export type OutputListDataItemKey = Extract<keyof OutputListDataItem, string>;

export interface FilterConfig {
    field: OutputListDataItemKey;
    value: string;
    type: FilterType;
    subField?: string;
}

export interface SortConfig {
    field: OutputListDataItemKey;
    ascending: boolean;
}

export interface ReportModal {
    id: number;
    type: 'report' | 'licenceSet';
    filename: string;
    licenceSetId?: string;
    processRunId: number;
    position: { top: number; left: number };
    size: { width: string; height: string };
}