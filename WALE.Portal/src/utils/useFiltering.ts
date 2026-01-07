import { useMemo, useState } from 'react';
import { OutputListDataItem } from '../api/generated/apiClient';
import type {FilterConfig, FilterType, OutputListDataItemKey, SortConfig} from "./types.ts";

export function useFiltering(data: OutputListDataItem[]) {
    const [filters, setFilters] = useState<Record<string, FilterConfig>>({});
    const [sortConfig, setSortConfig] = useState<SortConfig>({
        field: 'filename',
        ascending: true
    });

    // Apply a single filter
    const applyFilter = (field: string, value: string, type: FilterType, subField?: string) => {
        if (value === 'all' || value === 'All') {
            // Remove filter
            setFilters(prev => {
                const newFilters = { ...prev };
                delete newFilters[field];
                return newFilters;
            });
        } else {
            setFilters(prev => ({
                ...prev,
                [field]: { field: field as OutputListDataItemKey, value, type, subField }
            }));
        }
    };

    // Clear all filters except one
    const resetFiltersExcept = (exceptField?: string) => {
        if (exceptField) {
            setFilters(prev => {
                const kept = prev[exceptField];
                return kept ? { [exceptField]: kept } : {};
            });
        } else {
            setFilters({});
        }
    };

    // Toggle sort
    const toggleSort = (field: OutputListDataItemKey) => {
        setSortConfig(prev => ({
            field,
            ascending: prev.field === field ? !prev.ascending : true
        }));
    };

    // Filter logic - memoized
    const filteredAndSortedData = useMemo(() => {
        let result = [...data];

        // Apply all filters
        Object.values(filters).forEach(filter => {
            result = filterData(result, filter);
        });

        // Apply sorting
        result.sort((a, b) => {
            const aVal = a[sortConfig.field as OutputListDataItemKey];
            const bVal = b[sortConfig.field as OutputListDataItemKey];

            if (aVal === bVal) return 0;

            const comparison = aVal < bVal ? -1 : 1;
            return sortConfig.ascending ? comparison : -comparison;
        });

        return result;
    }, [data, filters, sortConfig]);

    return {
        filteredData: filteredAndSortedData,
        applyFilter,
        resetFiltersExcept,
        toggleSort,
        sortConfig,
        filters
    };
}

// Filtering logic
function filterData(
    items: OutputListDataItem[],
    filter: FilterConfig
): OutputListDataItem[] {
    const { field, value, type, subField } = filter;

    return items.filter(item => {
        const fieldValue = item[field as OutputListDataItemKey];

        switch (type) {
            case 'Year': {
                if (typeof fieldValue !== 'string') return false;
                const year = fieldValue.split('-')[0];
                return year === value;
            }

            case 'Bool': {
                if (value === 'true') return !!fieldValue;
                if (value === 'false') return !fieldValue;
                return true;
            }

            case 'EmptyOrNot': {
                if (value === 'populated') return !!fieldValue && fieldValue !== '';
                if (value === 'empty') return !fieldValue || fieldValue === '';
                return true;
            }

            case 'EmptyOrNotInt': {
                if (value === 'populated') return fieldValue !== 0;
                if (value === 'empty') return fieldValue === 0;
                return true;
            }

            case 'EmptyOrNotArray': {
                if (!Array.isArray(fieldValue)) return false;
                if (value === 'populated') return fieldValue.length > 0;
                if (value === 'empty') return fieldValue.length === 0;
                return true;
            }

            case 'ArrayValue': {
                if (!Array.isArray(fieldValue)) return false;
                return fieldValue.includes(value);
            }

            case 'ArrayValueMapped': {
                if (!Array.isArray(fieldValue) || !subField) return false;
                if (value === 'NoneSingle') {
                    return fieldValue.length >= 2;
                }
                return fieldValue.some((item: any) => item[subField] === value);
            }

            case 'ArrayValueArrayValueMapped': {
                if (!Array.isArray(fieldValue) || !subField) return false;
                if (value === 'Populated') return fieldValue.length > 0;
                if (value === 'Empty') return fieldValue.length === 0;

                return fieldValue.some((item: any) => {
                    const subArray = item[subField];
                    return Array.isArray(subArray) && subArray.includes(value);
                });
            }

            case 'Values':
            default: {
                return fieldValue === value;
            }
        }
    });
}