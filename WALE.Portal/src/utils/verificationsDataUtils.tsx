import { waleApiClient } from '../api/apiClient';
import { VerificationDataStatus } from '../api/generated/apiClient';

let verificationsDataCache: Promise<VerificationDataStatus> | undefined;
export function getVerificationDataStatus(
    forceRefresh = false
): Promise<VerificationDataStatus> {

    if (forceRefresh) {
        verificationsDataCache = undefined;
    }

    if (!verificationsDataCache) {
        verificationsDataCache = waleApiClient
            .getVerificationDataStatus()
            .catch(error => {
                verificationsDataCache = undefined;
                throw error;
            });
    }

    return verificationsDataCache;
}