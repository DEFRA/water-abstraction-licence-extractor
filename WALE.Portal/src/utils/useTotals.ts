import { useMemo } from 'react';
import { OutputListDataItem } from '../api/generated/apiClient';
import type {OutputListDataItemKey} from "./types.ts";

export interface Totals {
    filename: number;
    licenceNumber: number;
    licenceHolder: number;
    purposes: number;
    points: number;
    limitsCount: number;
    aggregatesCount: number;
    ocr: number;
    issueDate: number;
    issuer: number;
    meansFound: number;
    linkedLicences: number;
    licenceSectionVerifications: number;
    licenceSets: number;
    status: number;
}

export function useTotals(filteredData: OutputListDataItem[], sectionVerification : string): Totals {
    return useMemo(() => {
        return {
            filename: countNonEmpty(filteredData, 'filename', ''),
            licenceNumber: countNonEmpty(filteredData, 'licenceNumber', ''),
            licenceHolder: countNonEmpty(filteredData, 'licenceHolder', ''),
            purposes: countNonEmpty(filteredData, 'purposes', []),
            points: countNonEmpty(filteredData, 'points', []),
            limitsCount: countNonEmpty(filteredData, 'limitsCount', 0),
            aggregatesCount: countNonEmpty(filteredData, 'aggregatesCount', 0),
            ocr: countNonEmpty(filteredData, 'ocr', false),
            issueDate: countNonEmpty(filteredData, 'issueDate', ''),
            issuer: countNonEmpty(filteredData, 'issuer', ''),
            meansFound: countNonEmpty(filteredData, 'meansFound', false),
            linkedLicences: countNonEmpty(filteredData, 'linkedLicences', []),
            licenceSectionVerifications: countNonEmptyVerificationTypes(filteredData, sectionVerification),
            licenceSets: countNonEmptyLicenceSets(filteredData),
            status: filteredData.length // Status always has a value
        };
    }, [filteredData]);
}

function countNonEmpty(
    data: OutputListDataItem[],
    field: OutputListDataItemKey,
    emptyValue: any
): number {
    let count = 0;

    for (const item of data) {
        const value = item[field];

        // Handle arrays
        if (Array.isArray(emptyValue)) {
            if (Array.isArray(value) && value.length > 0) {
                count += value.length;
            }
            continue;
        }

        // Handle numbers
        if (typeof emptyValue === 'number') {
            if (value !== 0) {
                count++;
            }
            continue;
        }

        // Handle booleans
        if (typeof emptyValue === 'boolean') {
            if (value !== emptyValue) {
                count++;
            }
            continue;
        }

        // Handle strings
        if (value && value !== '') {
            count++;
        }
    }
    return count;
}

function countNonEmptyLicenceSets(data: OutputListDataItem[]): number {
    let count = 0;

    for (const item of data) {
        // Check if licenceSets exists and has more than just the first element
        if (item.licenceSets && item.licenceSets.length > 1) {
            count++;
        }
    }

    return count;
}

function countNonEmptyVerificationTypes(data: OutputListDataItem[], verificationType: string): number {
    let count = 0;

    for (const item of data) {

        const value = item["licenceSectionVerifications"];

        if (Array.isArray(value) && value.length > 0) {
            value.forEach(section => {
                const sectionItems = section.licenceSectionItems ?? [];

                if (verificationType === "") {
                    count += sectionItems.length;
                } else {
                    
                    if (verificationType === "Flagged")
                    {
                        count += sectionItems.filter(sectionItem =>
                            sectionItem.scrapedDataIsDifferent
                        ).length;
                    }
                    else {
                        count += sectionItems.filter(sectionItem =>
                            sectionItem.verificationTypes?.includes(verificationType)
                        ).length;
                    }
                }
            });
        }
       }

    return count;
}