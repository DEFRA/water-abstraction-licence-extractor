import { waleApiClient } from '../api/apiClient';

let documentSectionsCache: Promise<string[]> | undefined;
let linkReasonsCache: Promise<string[]> | undefined;

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