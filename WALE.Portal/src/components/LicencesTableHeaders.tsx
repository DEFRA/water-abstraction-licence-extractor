import {useMemo} from 'react';
import {OutputListDataItem} from '../api/generated/apiClient';
import {FilterSelect} from './FilterSelect';
import type {FilterConfig, FilterType, OutputListDataItemKey} from "../utils/types.ts";

interface LicencesTableHeadersProps {
    data: OutputListDataItem[];
    onFilterChange: (field: string, value: string, type: FilterType, subField?: string) => void;
    onResetFilters: (exceptField?: string) => void;
    onToggleSort: (field: OutputListDataItemKey) => void;
    onToggleSingles: (checked: boolean) => void;
    filters: Record<string, FilterConfig>;
    showSingles: boolean;
}

export function LicencesTableHeaders({
                                         data,
                                         onFilterChange,
                                         onResetFilters,
                                         onToggleSort,
                                         onToggleSingles,
                                         filters,
                                         showSingles
                                     }: LicencesTableHeadersProps) {

    // Generate unique issuers for dropdown
    const uniqueIssuers = useMemo(() => {
        const issuers = new Set(data.map(item => item.issuer).filter(Boolean));
        return ['All', ...Array.from(issuers).sort()];
    }, [data]);

    // Generate unique years for dropdown
    const uniqueYears = useMemo(() => {
        const years = new Set(
            data
                .map(item => item.issueDate?.split('-')[0])
                .filter(year => year && parseInt(year) >= 1900)
        );
        return ['All', ...Array.from(years).sort().reverse()];
    }, [data]);

    // Generate unique license sets for dropdown
    const uniqueLicenseSets = useMemo(() => {
        const sets = new Set<string>();
        data.forEach(item => {
            item.licenceSets?.slice(1).forEach(ls => {
                if (ls.shortLicenceSetId) {
                    sets.add(ls.shortLicenceSetId);
                }
            });
        });
        return ['All', ...Array.from(sets).sort()];
    }, [data]);

    const handleSort = (field: OutputListDataItemKey) => {
        onToggleSort(field);
    };

    return (
        <>
            <tr>
                <td colSpan={11}></td>
                <td>
                    <input
                        type="checkbox"
                        id="showSingles"
                        checked={showSingles}
                        onChange={(e) => onToggleSingles(e.target.checked)}
                    /> Show singles
                </td>
                <td colSpan={2}></td>
            </tr>
            <tr>
                <td style={{width: '5%'}}>&nbsp;</td>
                <td style={{width: '7%'}}>
                    Licence no. <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceNumber'); }}>&#8693;</a>
                </td>
                <td style={{width: '5%'}} className="default-hidden">
                    Licence holder <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceHolder'); }}>&#8693;</a>
                </td>
                <td style={{width: '14%'}}>
                    Purposes <a href="#" onClick={(e) => { e.preventDefault(); handleSort('purposes'); }}>&#8693;</a>
                </td>
                <td style={{width: '14%'}}>
                    Points <a href="#" onClick={(e) => { e.preventDefault(); handleSort('points'); }}>&#8693;</a>
                </td>
                <td style={{width: '7%'}}>
                    Abs. limits <a href="#" onClick={(e) => { e.preventDefault(); handleSort('limitsCount'); }}>&#8693;</a>
                </td>
                <td style={{width: '5%'}}>
                    Agg's <a href="#" onClick={(e) => { e.preventDefault(); handleSort('aggregatesCount'); }}>&#8693;</a>
                </td>
                <td style={{width: '5%'}}>
                    Scan <a href="#" onClick={(e) => { e.preventDefault(); handleSort('ocr'); }}>&#8693;</a>
                </td>
                <td style={{width: '5%'}}>
                    Iss. date <a href="#" onClick={(e) => { e.preventDefault(); handleSort('issueDate'); }}>&#8693;</a>
                </td>
                <td style={{width: '6%'}}>
                    Issuer <a href="#" onClick={(e) => { e.preventDefault(); handleSort('issuer'); }}>&#8693;</a>
                </td>
                <td style={{width: '7%'}}>
                    Means of abs. <a href="#" onClick={(e) => { e.preventDefault(); handleSort('meansFound'); }}>&#8693;</a>
                </td>
                <td style={{width: '7%'}}>
                    Linked&nbsp;licences <a href="#" onClick={(e) => { e.preventDefault(); handleSort('linkedLicences'); }}>&#8693;</a>
                </td>
                <td style={{width: '8%'}}>
                    Licence sets <a href="#" onClick={(e) => { e.preventDefault(); handleSort('licenceSets'); }}>&#8693;</a>
                </td>
                <td style={{width: '10%'}}>
                    Verified <a href="#" onClick={(e) => { e.preventDefault(); handleSort('latestLicenceSectionVerifications'); }}>&#8693;</a>
                </td>
            </tr>
        </>
    );
}

export default LicencesTableHeaders;