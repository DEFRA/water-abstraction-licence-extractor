import type {LicenceEstateQuery} from "../class/LicenceEstateQuery.tsx";

/// One of Paging's own options, or its selector shows a size the page is not using.
export const DefaultPageSize = 100;

const StringKeys = [
    'searchTerm',
    'issuer',
    'aggregatesFilter',
    'ShortLicenceSetId',
    'linkedLicencesType',
    'verificationType',
    'sortField',
    'projectedStatus'
] as const;

const BooleanKeys = [
    'limitsEmpty',
    'purposesEmpty',
    'pointsEmpty',
    'ocrScan',
    'meansFound',
    'isLicenceNumberFlagged',
    'isIssueDateFlagged'
] as const;

const NumberKeys = ['issueYear', 'queue', 'processRunId'] as const;

export function queryFromParams(params: URLSearchParams): LicenceEstateQuery {
    const query: LicenceEstateQuery = {
        skip: 0,
        take: Number(params.get('size')) || DefaultPageSize,
        sortAscending: params.get('sortAscending') !== 'false',
        licenceNumbers: params.getAll('licenceNumber')
    };

    const page = Number(params.get('page')) || 1;
    query.skip = (page - 1) * query.take;

    for (const key of StringKeys) {
        const value = params.get(key);

        if (value !== null) {
            query[key] = value;
        }
    }

    for (const key of BooleanKeys) {
        const value = params.get(key);

        if (value !== null) {
            query[key] = value === 'true';
        }
    }

    for (const key of NumberKeys) {
        const value = params.get(key);

        if (value !== null && value !== '') {
            query[key] = Number(value);
        }
    }

    return query;
}

export function paramsFromQuery(query: LicenceEstateQuery): URLSearchParams {
    const params = new URLSearchParams();

    for (const key of StringKeys) {
        const value = query[key];

        if (value) {
            params.set(key, value);
        }
    }

    for (const key of BooleanKeys) {
        const value = query[key];

        if (value !== undefined) {
            params.set(key, String(value));
        }
    }

    for (const key of NumberKeys) {
        const value = query[key];

        if (value !== undefined) {
            params.set(key, String(value));
        }
    }

    for (const licenceNumber of query.licenceNumbers ?? []) {
        params.append('licenceNumber', licenceNumber);
    }

    if (!query.sortAscending) {
        params.set('sortAscending', 'false');
    }

    if (query.take !== DefaultPageSize) {
        params.set('size', String(query.take));
    }

    const page = Math.floor(query.skip / query.take) + 1;

    if (page > 1) {
        params.set('page', String(page));
    }

    return params;
}
