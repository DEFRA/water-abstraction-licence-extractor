import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
import UnorderedListOfStrings from "./UnorderedListOfStrings.tsx";
import {getThumbnailUrl} from "../utils/images.ts";
import LicenceSetsList from "./LicenceSetsList.tsx";
import LinkedLicencesList from "./LinkedLicencesList.tsx";

interface OutputItemTableRowProps {
    item: OutputListDataItem;
    data: OutputListDataItem[];
    oddRow: boolean;
}

function LicencesTableRow({item, data, oddRow}: OutputItemTableRowProps) {
    return (
        <tr style={{backgroundColor: oddRow ? '#F6F6F6' : '#FAFAFA'}}>
            <td style={{textAlign: 'center'}}>
                <img
                    src={getThumbnailUrl(item.imagePath ?? "")}
                    style={{height: '80px'}}
                    alt='No image found'
                    onError={(e) => e.currentTarget.style.display = 'none'}/>
            </td>
            <td><a href={'report.html?filename=${item.filename}'}
                   onClick={(e) => {
                       e.preventDefault();
                       // openIframe(item.filename);
                   }}>{item.filename}</a>
            </td>
            <td id={dashesIfNullOrEmpty(item.licenceNumber)}>{dashesIfNullOrEmpty(item.licenceNumber)}</td>
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
                <LinkedLicencesList item={item} data={data} />
            </td>
            <td className='licenceSetTd'>
                <LicenceSetsList item={item}/>
                <span className='noLicenceSetsShowing'>--</span>
            </td>
            <td>{item.status}</td>
        </tr>
    );
}

export default LicencesTableRow;