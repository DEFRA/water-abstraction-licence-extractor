export type ProcessRunQuery = {
    searchTerm?: string;
    skip: number;
    take: number;
    issuer?: string;
    limitsEmpty?: boolean;
    aggregatesEmpty?: boolean;
    purposesEmpty?: boolean;
    pointsEmpty?: boolean;
    ocrScan?: boolean;
    issueYear?: number;
    meansFound?: boolean;
    licenceSets?: string;
    linkedLicencesType?: string;
    verified?: string;
};