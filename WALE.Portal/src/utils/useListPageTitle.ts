import {useEffect} from 'react';
import {RegionOption} from '../api/generated/apiClient';

const APP_TITLE = 'WALE Portal';

export function useListPageTitle(processRunId: number, regionId: number | undefined, regions: RegionOption[]) {
    const regionName = regions.find(region => region.regionId === regionId)?.regionName;

    useEffect(() => {
        document.title = [`PR ${processRunId}`, regionName, APP_TITLE]
            .filter(Boolean)
            .join(' | ');

        return () => {
            document.title = APP_TITLE;
        };
    }, [processRunId, regionName]);
}
