import { waleApiClient } from './apiClient';
import type { LicenceEstateQuery } from '../class/LicenceEstateQuery.tsx';

/// NSwag flattens the query object into 26 positional parameters, so the order is pinned here
/// once rather than at every call site.
export function getLicenceEstate(query: LicenceEstateQuery) {
    return waleApiClient.getLicenceEstate(
        query.projectedStatus,
        query.processRunId,
        query.queue,
        query.minimumDocumentCount,
        query.maximumRunCount,
        query.searchTerm,
        undefined,
        query.skip,
        query.take,
        query.issuer,
        query.limitsEmpty,
        query.aggregatesFilter,
        query.aggregatePrimaryTypeFilter,
        query.ocrScan,
        query.purposesEmpty,
        query.pointsEmpty,
        query.issueYear,
        query.meansFound,
        query.isLicenceNumberFlagged,
        query.isIssueDateFlagged,
        query.ShortLicenceSetId,
        query.linkedLicencesType,
        query.verificationType,
        query.sortField,
        query.sortAscending,
        query.licenceNumbers);
}
