import {createContext, useContext, useEffect, useMemo, useState, type ReactNode} from 'react';
import {waleApiClient} from '../api/apiClient';
import {getFileId as lookupFileId} from './verificationUtils.ts';

const fileIdToLicenceNumberMapCache = new Map<number, Promise<Record<string, string>>>();

function fetchFileIdToLicenceNumberMap(processRunId: number): Promise<Record<string, string>> {
    let cached = fileIdToLicenceNumberMapCache.get(processRunId);

    if (!cached) {
        cached = waleApiClient.getLicenceFileIdMap(processRunId);
        fileIdToLicenceNumberMapCache.set(processRunId, cached);
    }

    return cached;
}

interface FileIdMapContextValue {
    getFileId: (licenceNumber: string | undefined) => string | false;
    getLicenceNumber: (fileId: string | undefined) => string | undefined;
}

const FileIdMapContext = createContext<FileIdMapContextValue>({
    getFileId: () => false,
    getLicenceNumber: () => undefined
});

interface FileIdMapProviderProps {
    processRunId: number;
    children: ReactNode;
}

export function FileIdMapProvider({processRunId, children}: FileIdMapProviderProps) {
    const [fileIdToLicenceNumber, setFileIdToLicenceNumber] = useState<Record<string, string> | undefined>(undefined);

    useEffect(() => {
        let cancelled = false;
        setFileIdToLicenceNumber(undefined);

        fetchFileIdToLicenceNumberMap(processRunId)
            .then(map => {
                if (!cancelled) {
                    setFileIdToLicenceNumber(map);
                }
            })
            .catch(err => {
                console.error('Error fetching licence fileId map:', err);
            });

        return () => {
            cancelled = true;
        };
    }, [processRunId]);

    const licenceNumberToFileId = useMemo(() => {
        const inverted: Record<string, string> = {};

        for (const fileId in fileIdToLicenceNumber) {
            inverted[fileIdToLicenceNumber[fileId]] = fileId;
        }

        return inverted;
    }, [fileIdToLicenceNumber]);

    const value = useMemo<FileIdMapContextValue>(() => ({
        getFileId: (licenceNumber: string | undefined) => lookupFileId(licenceNumberToFileId, licenceNumber),
        getLicenceNumber: (fileId: string | undefined) => (fileId ? fileIdToLicenceNumber?.[fileId] : undefined)
    }), [licenceNumberToFileId, fileIdToLicenceNumber]);

    return (
        <FileIdMapContext.Provider value={value}>
            {children}
        </FileIdMapContext.Provider>
    );
}

export function useFileIdMap() {
    return useContext(FileIdMapContext);
}
