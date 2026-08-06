export type ProcessRunQuery = {
    searchTerm?: string;
    skip: number;
    take: number;
    issuer?: string;
    limitsEmpty?: boolean;
    aggregatesFilter?: string;
    purposesEmpty?: boolean;
    pointsEmpty?: boolean;
    ocrScan?: boolean;
    issueYear?: number;
    meansFound?: boolean;
    ShortLicenceSetId?: string;
    linkedLicencesType?: string;
    verificationType?: string;
    sortField?: string;
    sortAscending?: boolean;
    licenceNumbers?: string[];
};