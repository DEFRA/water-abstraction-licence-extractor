import {LicenceFileMapEntry, OutputListDataItem} from "../api/generated/apiClient.ts";

export const isScrapedDataDifferent = (
    outputListDataItem: OutputListDataItem | undefined,
    licenceSectionName: string,
    licenceSectionItemId: string | undefined,
): boolean => {
    if (!outputListDataItem || !licenceSectionItemId) return false;
    return (outputListDataItem.licenceSectionVerifications ?? [])
        .filter(s => s.licenceSectionName === licenceSectionName)
        .flatMap(s => s.licenceSectionItems ?? [])
        .some(i => i.licenceSectionItemId === licenceSectionItemId && !!i.scrapedDataIsDifferent);
};

export const getVerificationTypeColor = (type: string): string =>
    getVerificationTypeBackgroundColor(type);

export const getVerificationWithNotesFirstPart = (value: string): string => {
    return value.split("::")[0];
};

export const getVerificationTypeBackgroundColor = (type: string): string => {
    switch (type) {
        case 'AutoConfirm':
            return '#00D100';
        case 'Confirmed':
            return '#008000';
        case 'CompleteBusinessReview':
            return '#004700';
        case 'Removed':
            return 'red';
        case 'AutoFail':
            return 'red';
        case 'Edited':
            return 'blue';
        case 'Added':
            return '#1890ff';
        case 'AutoWarn':
            return 'darkorange';
        case 'RequestBusinessReview':
            return 'darkorange';
        default:
            return 'inherit';
    }
};

export const getVerificationTypeInitials = (type: string): string => {
    switch (type) {
        case 'Confirmed':
            return 'C';
        case 'AutoConfirm':
            return 'AC';
        case 'Removed':
            return 'X';
        case 'Edited':
            return 'E';
        case 'Added':
            return '+';
        case 'AutoFail':
            return 'AF';
        case 'AutoWarn':
            return 'AW';
        case 'RequestBusinessReview':
            return 'BR';
        case 'CompleteBusinessReview':
            return 'BC';
        default:
            return '';
    }
};

export const hasOnlyOneOutgoingSection = (containedIn?: any[]): boolean => {
    if (!containedIn) return true;
    return containedIn.filter(s => s.direction === 'Outgoing').length <= 1;
};

export const hasAnyOutgoingSections = (containedIn?: any[]): boolean => {
    if (!containedIn) return false;
    return containedIn.filter(s => s.direction === 'Outgoing').length > 0;
};

export function getFileId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): string | false {
    if (!licenceNumber || !fileIdMap) {
        return false;
    }

    let list = fileIdMap[licenceNumber];
    
    if (!list) {
        return false;
    }
    
    return list[0].fileId ?? false;
}

export function getLicenceId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): number | undefined {
    if (!licenceNumber || !fileIdMap) {
        return undefined;
    }

    let list = fileIdMap[licenceNumber];

    if (!list) {
        return undefined;
    }

    return list[0].licenceId;
}

export function getMatchesResultId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): number | undefined {
    if (!licenceNumber || !fileIdMap) {
        return undefined;
    }

    let list = fileIdMap[licenceNumber];

    if (!list) {
        return undefined;
    }

    return list[0].matchesResultId;
}
