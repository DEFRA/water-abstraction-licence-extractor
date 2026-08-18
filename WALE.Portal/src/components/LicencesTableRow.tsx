import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {getVerificationTypeBackgroundColor, getVerificationTypeInitials} from "../utils/verificationUtils.ts";
import {dashesIfNull, dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
import UnorderedListOfStrings from "./UnorderedListOfStrings";
import LicenceSetsList from "./LicenceSetsList";
import LinkedLicencesList from "./LinkedLicencesList";

interface OutputItemTableRowProps {
    item: OutputListDataItem;
    oddRow: boolean;
    onOpenReport: (fileId: string) => void;
    onOpenLicenceSetReport: (fileId: string, licenceSetId: string) => void;
    showSingles: boolean;
}

function LicencesTableRow({item, oddRow, onOpenReport, onOpenLicenceSetReport, showSingles}: OutputItemTableRowProps) {
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
            <td>
                <strong>File:</strong> {(item.aggregatesCount ?? 0) > 0 ? "True" : "False"} ({dashesIfNull(item.aggregatesCount)})
                <br /><strong>Nald:</strong> {item.naldHasAggregateCondition ? "True" : "False"}</td>
            <td>{(item.ocr ? "True" : "False")}</td>
            <td>{dashesIfNullOrEmpty(item.issueDate)}</td>
            <td>{dashesIfNullOrEmpty(item.issuer)}</td>
            <td>{(item.meansFound ? "True" : "False")}</td>
            <td>
                <LinkedLicencesList
                    item={item}
                    onOpenReport={onOpenReport}
                />
            </td>
            <td className='licenceSetTd default-hidden'>
                <LicenceSetsList 
                    item={item} 
                    onOpenLicenceSetReport={onOpenLicenceSetReport}
                    showSingles={showSingles}
                />
            </td>
            <td>
                {((item.licenceSectionVerifications?.length ?? 0) > 0 ?
                    item.licenceSectionVerifications!.map((section) => (
                        <div key={section.licenceSectionName} style={{ marginBottom: '10px' }}>
                            <strong>{section.licenceSectionName}</strong>
                            <UnorderedListOfStrings items={
                                (section.licenceSectionItems || []).map((v) => {
                                    const itemId = v.licenceSectionItemId ?? "Unknown";
                                    return (
                                        <span key={itemId}>
                                            {itemId}{' '}
                                            {(v.verificationTypes || []).map((vt: string, idx: number) => (
                                                <span key={idx} title={vt ?? ''} style={{
                                                    backgroundColor: getVerificationTypeBackgroundColor(vt),
                                                    color: 'white',
                                                    fontSize: '0.7em',
                                                    padding: '1px 3px',
                                                    borderRadius: '3px',
                                                    marginRight: '2px',
                                                    verticalAlign: 'middle',
                                                    fontWeight: 'bold',
                                                    fontFamily: 'sans-serif'
                                                }}>
                                                    {getVerificationTypeInitials(vt)}
                                                </span>
                                            ))}
                                            {v.scrapedDataIsDifferent && '🚩'}
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