import { waleApiClient } from '../api/apiClient';
import { ProcessRun } from '../api/generated/apiClient';

let documentSectionsCache: Promise<string[]> | undefined;
export function getDocumentSections(): Promise<string[]> {
    if (!documentSectionsCache) {
        documentSectionsCache = waleApiClient
            .getDocumentSections()
            .catch(error => {
                documentSectionsCache = undefined;
                throw error;
            });
    }

    return documentSectionsCache;
}

let linkReasonsCache: Promise<string[]> | undefined;
export function getLinkReasons(): Promise<string[]> {
    if (!linkReasonsCache) {
        linkReasonsCache = waleApiClient
            .getLinkReasons()
            .catch(error => {
                linkReasonsCache = undefined;
                throw error;
            });
    }

    return linkReasonsCache;
}

let processRunsCache: Promise<ProcessRun[]> | undefined;
export function getProcessRuns(): Promise<ProcessRun[]> {
    if (!processRunsCache) {
        processRunsCache = waleApiClient
            .getProcessRuns()
            .catch(error => {
                processRunsCache = undefined;
                throw error;
            });
    }

    return processRunsCache;
}