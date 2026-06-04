import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
import UnorderedListOfStrings from "./UnorderedListOfStrings";
import LicenceSetsList from "./LicenceSetsList";
import LinkedLicencesList from "./LinkedLicencesList";
import {getThumbnailUrl} from "../utils/images.ts";

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
                <img
                    loading='lazy'
                    src={getThumbnailUrl(item.fileId ?? "")}
                    style={{height: '80px'}}
                    alt='No image found'
                    onError={(e) => e.currentTarget.style.display = 'none'}/>
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
                            <UnorderedListOfStrings items={verifications!.map(v => {
                                let color = 'inherit';
                                if (v.verificationType === 'Confirmed') color = 'green';
                                else if (v.verificationType === 'AutoPass') color = 'green';
                                else if (v.verificationType === 'Removed') color = 'red';
                                else if (v.verificationType === 'Edited') color = 'darkorange';
                                else if (v.verificationType === 'Added') color = 'blue';
                                else if (v.verificationType === 'AutoFail') color = 'red';
                                else if (v.verificationType === 'AutoWarn') color = 'darkorange';

                                return <span style={{color}}>{`${v.licenceSectionItemId} - ${v.verificationType}`}{v.scrapedDataIsDifferent && ' 🚩'}</span>;
                            })}/>
                        </div>
                    ))
                    : 'No verifications')}
            </td>
        </tr>
    );
}

export default LicencesTableRow;