import {createContext, useContext, useEffect, useMemo, useState, type ReactNode} from 'react';
import {waleApiClient} from '../api/apiClient';
import {
    getFileId as lookupFileId,
    getLicenceId as lookupLicenceId,
    getMatchesResultId as lookupMatchesResultId
} from './verificationUtils.ts';
import type {LicenceFileMapEntry} from "../api/generated/apiClient.ts";

const fileIdToLicenceNumberMapCache = new Map<number, Promise<Record<string, LicenceFileMapEntry[]>>>();

function fetchFileIdToLicenceNumberMap(processRunId: number): Promise<Record<string, LicenceFileMapEntry[]>> {
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
    getLicenceId: (licenceNumber: string | undefined) => number | undefined;
    getMatchesResultId: (licenceNumber: string | undefined) => number | undefined;
}

const FileIdMapContext = createContext<FileIdMapContextValue>({
    getFileId: () => false,
    getLicenceNumber: () => undefined,
    getLicenceId: () => -1,
    getMatchesResultId: () => -1
});

interface FileIdMapProviderProps {
    processRunId: number;
    children: ReactNode;
}

export function FileIdMapProvider({processRunId, children}: FileIdMapProviderProps) {
    const [fileIdToLicenceNumber, setFileIdToLicenceNumber] = useState<Record<string, LicenceFileMapEntry[]> | undefined>(undefined);

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
        const licenceNumberToFileIdMap: Record<string, LicenceFileMapEntry[]> = {};

        for (const fileId in fileIdToLicenceNumber) {
            let list = fileIdToLicenceNumber[fileId];
            let licenceNumber = list[0].licenceNumber!;
            
            licenceNumberToFileIdMap[licenceNumber] = list;
        }

        return licenceNumberToFileIdMap;
    }, [fileIdToLicenceNumber]);

    const value = useMemo<FileIdMapContextValue>(() => ({
        getFileId: (licenceNumber: string | undefined) => lookupFileId(licenceNumberToFileId, licenceNumber),
        getLicenceNumber: (fileId: string | undefined) => (fileId ? fileIdToLicenceNumber?.[fileId][0].licenceNumber : undefined),
        getLicenceId: (licenceNumber: string | undefined) => lookupLicenceId(licenceNumberToFileId, licenceNumber),
        getMatchesResultId: (licenceNumber: string | undefined) => lookupMatchesResultId(licenceNumberToFileId, licenceNumber)
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