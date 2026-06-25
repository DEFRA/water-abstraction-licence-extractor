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
                const arrayValue = Array.isArray(fieldValue) ? fieldValue : [];
                if (value === 'populated') return arrayValue.length > 0;
                if (value === 'empty') return arrayValue.length === 0;
                return true;
            }

            case 'ArrayValue': {
                if (!Array.isArray(fieldValue)) return false;
                return fieldValue.includes(value);
            }

            case 'ArrayValueMapped': {
                const arrayValue = Array.isArray(fieldValue) ? fieldValue : [];
                if (!subField) return false;
                if (value === 'NoneSingle') {
                    return arrayValue.length >= 2;
                }
                return arrayValue.some((item: any) => item[subField] === value);
            }

            case 'ArrayValueArrayValueMapped': {
                const arrayValue = Array.isArray(fieldValue) ? fieldValue : [];
                if (!subField) return false;
                if (value === 'Populated') return arrayValue.length > 0;
                if (value === 'Empty') return arrayValue.length === 0;

                return arrayValue.some((item: any) => {
                    const subArray = item[subField];
                    return Array.isArray(subArray) && subArray.includes(value);
                });
            }

            case 'LicenceVerification': {
                const arrayValue = Array.isArray(fieldValue) ? fieldValue : [];
                if (value === 'populated') return arrayValue.length > 0;
                if (value === 'empty') return arrayValue.length === 0;
                
                return arrayValue.some((v: any) => v.verificationType === value);
            }

            case 'Values':
            default: {
                return fieldValue === value;
            }
        }
    });
}