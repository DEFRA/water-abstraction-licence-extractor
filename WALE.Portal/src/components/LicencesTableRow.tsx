import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
import UnorderedListOfStrings from "./UnorderedListOfStrings";
import LicenceSetsList from "./LicenceSetsList";
import LinkedLicencesList from "./LinkedLicencesList";

interface OutputItemTableRowProps {
    item: OutputListDataItem;
    data: OutputListDataItem[];
    oddRow: boolean;
    onOpenReport: (fileId: string) => void;
    onOpenLicenceSetReport: (fileId: string, licenceSetId: string) => void;
    showSingles: boolean;
}

function LicencesTableRow({item, data, oddRow, onOpenReport, onOpenLicenceSetReport, showSingles}: OutputItemTableRowProps) {
    return (
        <tr style={{backgroundColor: oddRow ? '#F6F6F6' : '#FAFAFA'}}>
            <td style={{textAlign: 'center'}}>
                .
            </td>
            <td id={dashesIfNullOrEmpty(item.licenceNumber)}>
                <a href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(item.fileId!);
                   }}
                   dangerouslySetInnerHTML={{ __html: dashesIfNullOrEmpty(item.licenceNumber) }} />
            </td>
            <td className='default-hidden'>{dashesIfNullOrEmpty(item.licenceHolder)}</td>
            <td>{((item.purposes?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.purposes!}/> : '--')}</td>
            <td>{((item.points?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.points!}/> : '--')}</td>
            <td>{dashesIfNullOrZero(item.limitsCount)}</td>
            <td>{dashesIfNullOrZero(item.aggregatesCount)}</td>
            <td>{(item.ocr ? "True" : "False")}</td>
            <td>{dashesIfNullOrEmpty(item.issueDate)}</td>
            <td>{dashesIfNullOrEmpty(item.issuer)}</td>
            <td>{(item.meansFound ? "True" : "False")}</td>
            <td>
                <LinkedLicencesList 
                    item={item} 
                    data={data} 
                    onOpenReport={onOpenReport}
                />
            </td>
            <td className='licenceSetTd'>
                <LicenceSetsList 
                    item={item} 
                    onOpenLicenceSetReport={onOpenLicenceSetReport}
                    showSingles={showSingles}
                />
            </td>
            <td>
                {((item.latestLicenceSectionVerifications?.length ?? 0) > 0 ?
                    Object.entries(
                        item.latestLicenceSectionVerifications!.reduce((acc, v) => {
                            const key = v.licenceSectionName ?? "Unknown";
                            if (!acc[key]) acc[key] = [];
                            acc[key].push(v);
                            return acc;
                        }, {} as Record<string, typeof item.latestLicenceSectionVerifications>)
                    ).map(([sectionName, verifications]) => (
                        <div key={sectionName} style={{ marginBottom: '10px' }}>
                            <strong>{sectionName}</strong>
                            <UnorderedListOfStrings items={
                                Object.entries(
                                    verifications!.reduce((acc, v) => {
                                        const key = v.licenceSectionItemId ?? "Unknown";
                                        if (!acc[key]) acc[key] = [];
                                        acc[key].push(v);
                                        return acc;
                                    }, {} as Record<string, typeof item.latestLicenceSectionVerifications>)
                                ).map(([itemId, itemVerifications]) => {
                                    const sortedVerifications = [...itemVerifications!].sort((a, b) => {
                                        const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
                                        const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
                                        return dateA - dateB;
                                    });

                                    return (
                                        <span key={itemId}>
                                            {itemId}{' '}
                                            {sortedVerifications.map((v, i) => {
                                                let color = 'inherit';
                                                let initials = '';
                                                if (v.verificationType === 'Confirmed') { color = 'inherit'; initials = '✅'; }
                                                else if (v.verificationType === 'AutoConfirm') { color = 'inherit'; initials = '✅'; }
                                                else if (v.verificationType === 'Removed') { color = 'inherit'; initials = '❌'; }
                                                else if (v.verificationType === 'Edited') { color = 'inherit'; initials = '✏️'; }
                                                else if (v.verificationType === 'Added') { color = 'inherit'; initials = '➕'; }
                                                else if (v.verificationType === 'AutoFail') { color = 'inherit'; initials = '❌'; }
                                                else if (v.verificationType === 'AutoWarn') { color = 'inherit'; initials = '⚠'; }

                                                return (
                                                    <span key={i}>
                                                        <span title={v.verificationType ?? ''} style={{
                                                            backgroundColor: color,
                                                            color: 'white',
                                                            fontSize: '0.7em',
                                                            padding: '1px 3px',
                                                            borderRadius: '3px',
                                                            marginRight: '2px',
                                                            verticalAlign: 'middle',
                                                            fontWeight: 'bold',
                                                            fontFamily: 'sans-serif'
                                                        }}>
                                                            {initials}
                                                        </span>
                                                        {i === sortedVerifications.length-1 && v.scrapedDataIsDifferent && '🚩'}
                                                    </span>
                                                );
                                            })}
                                        </span>
                                    );
                                })
                            }/>
                        </div>
                    ))
                    : 'No verifications')}
            </td>
        </tr>
    );
}

export default LicencesTableRow;