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
    onOpenReport: (filename: string) => void;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
    showSingles: boolean;
}

function LicencesTableRow({item, data, oddRow, onOpenReport, onOpenLicenceSetReport, showSingles}: OutputItemTableRowProps) {
    return (
        <tr style={{backgroundColor: oddRow ? '#F6F6F6' : '#FAFAFA'}}>
            <td style={{textAlign: 'center'}}>
                <img
                    src={getThumbnailUrl(item.fileId ?? "")}
                    style={{height: '80px'}}
                    alt='No image found'
                    onError={(e) => e.currentTarget.style.display = 'none'}/>
            </td>
            <td className='filename-cell'><a href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(item.filename!);
                   }}>{item.filename}</a>
            </td>
            <td id={dashesIfNullOrEmpty(item.licenceNumber)} dangerouslySetInnerHTML={{ __html: dashesIfNullOrEmpty(item.licenceNumber) }} />
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
            <td>{item.status}</td>
            <td>
                {((item.licenceVerificationSummary?.length ?? 0) > 0 ?
                    <UnorderedListOfStrings items={item.licenceVerificationSummary!.map(v => `${v.licenceSectionName}: ${v.verificationType}`)}/>
                    : '--')}
            </td>
        </tr>
    );
}

export default LicencesTableRow;