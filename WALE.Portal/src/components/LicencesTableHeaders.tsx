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
                <td></td>
                <td>
                    <FilterSelect
                        id="licence-number-filter"
                        field="licenceNumber"
                        type="EmptyOrNot"
                        value={filters['licenceNumber']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('licenceNumber')}
                        options={[
                            { value: 'all', label: 'All' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td className="default-hidden">
                    <FilterSelect
                        id="licence-holder-filter"
                        field="licenceHolder"
                        type="EmptyOrNot"
                        value={filters['licenceHolder']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('licenceHolder')}
                        options={[
                            { value: 'all', label: 'All' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="purposes-filter"
                        field="purposes"
                        type="EmptyOrNotArray"
                        value={filters['purposes']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('purposes')}
                        options={[
                            { value: 'all', label: 'Any amount' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="points-filter"
                        field="points"
                        type="EmptyOrNotArray"
                        value={filters['points']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('points')}
                        options={[
                            { value: 'all', label: 'Any amount' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="limits-filter"
                        field="limitsCount"
                        type="EmptyOrNotInt"
                        value={filters['limitsCount']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('limitsCount')}
                        options={[
                            { value: 'all', label: 'Any amount' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="aggregates-filter"
                        field="aggregatesCount"
                        type="EmptyOrNotInt"
                        value={filters['aggregatesCount']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('aggregatesCount')}
                        options={[
                            { value: 'all', label: 'Any amount' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="ocr-filter"
                        field="ocr"
                        type="Bool"
                        value={filters['ocr']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('ocr')}
                        options={[
                            { value: 'all', label: 'All' },
                            { value: 'true', label: 'True' },
                            { value: 'false', label: 'False' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="issue-date-filter"
                        field="issueDate"
                        type="Year"
                        value={filters['issueDate']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('issueDate')}
                        options={uniqueYears.map(y => ({ value: y!, label: y! }))}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="issuer-filter"
                        field="issuer"
                        type="Values"
                        value={filters['issuer']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('issuer')}
                        options={uniqueIssuers.map(i => ({ value: i!, label: i! }))}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="means-filter"
                        field="meansFound"
                        type="Bool"
                        value={filters['meansFound']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('meansFound')}
                        options={[
                            { value: 'all', label: 'All' },
                            { value: 'true', label: 'True' },
                            { value: 'false', label: 'False' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="linked-licences-filter"
                        field="linkedLicences"
                        type="ArrayValueArrayValueMapped"
                        subField="fromSection"
                        value={filters['linkedLicences']?.value ?? 'All'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('linkedLicences')}
                        options={[
                            { value: 'All', label: 'All' },
                            { value: 'AbstractionLimits', label: 'In Limits' },
                            { value: 'FurtherConditions', label: 'In Further Conditions' },
                            { value: 'AdditionalInformation', label: 'In Additional' },
                            { value: 'Records', label: 'In Records' },
                            { value: 'ImplicitBackLink', label: 'Implicit back link' },
                            { value: 'Populated', label: 'Not empty' },
                            { value: 'Empty', label: '--' }
                        ]}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="licence-sets-filter"
                        field="licenceSets"
                        type="ArrayValueMapped"
                        subField="shortLicenceSetId"
                        value={filters['licenceSets']?.value ?? 'All'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('licenceSets')}
                        options={uniqueLicenseSets.map(s => ({ value: s, label: s }))}
                    />
                </td>
                <td>
                    <FilterSelect
                        id="verified-filter"
                        field="latestLicenceSectionVerifications"
                        type="EmptyOrNotArray"
                        value={filters['latestLicenceSectionVerifications']?.value ?? 'all'}
                        onChange={onFilterChange}
                        onReset={() => onResetFilters('latestLicenceSectionVerifications')}
                        options={[
                            { value: 'all', label: 'All' },
                            { value: 'populated', label: 'Not empty' },
                            { value: 'empty', label: '--' }
                        ]}
                    />
                </td>
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