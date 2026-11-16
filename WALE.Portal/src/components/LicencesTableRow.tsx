import {ContainedInInformation, InformationSource, OutputListDataItem} from "../api/generated/apiClient.ts";
import {
    getVerificationTypeBackgroundColor,
    getVerificationTypeInitials,
    getVerificationWithNotesFirstPart,
    getVerificationWithNotesTooltip
} from "../utils/verificationUtils.ts";
import {compareAlphanumeric, dashesIfNull, dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
import UnorderedListOfStrings from "./UnorderedListOfStrings";
import LicenceSetsList from "./LicenceSetsList";
import LinkedLicencesList from "./LinkedLicencesList";
import LicenceNumberFlagTag from "./LicenceNumberFlagTag";
import {getVerificationSectionsToDisplay} from "../utils/verificationSections.ts";
import NaldTag from "./NaldTag.tsx";
import DocumentTag from "./DocumentTag.tsx";

interface OutputItemTableRowProps {
    item: OutputListDataItem;
    oddRow: boolean;
    onOpenReport: (fileId: string, licenceId: number, matchesResultId: number) => void;
    onOpenLicenceSetReport: (fileId: string, licenceId: number, matchesResultId: number, licenceSetId: string) => void;
    showSingles: boolean;
    verificationSections?: string[];
}

function LicencesTableRow({item, oddRow, onOpenReport, onOpenLicenceSetReport, showSingles, verificationSections}: OutputItemTableRowProps) {
    const sectionVerifications = item.licenceSectionVerifications ?? [];
    const sectionsToDisplay = getVerificationSectionsToDisplay(
        sectionVerifications.map(section => section.licenceSectionName),
        verificationSections);

    let toShortId = function(id: string){
        const parts = id.split("-");
        let sb = [];
        
        for (let i = 0; i < parts.length; i++) {
            let part = parts[i];
            let shortPart = '';
            
            if (i == 1) {
                shortPart = 'LV'
            }
            else if (i == 2 || i == parts.length - 1) {
                shortPart = part;
            }
            else {
                shortPart = part.substring(part.length - 3);
            }
            
            sb.push(shortPart);
        }
        
        return sb.join('-');
    }
    
    let naldCi = function(aggregateId : string) {
        let ci = new ContainedInInformation();
        ci.source = InformationSource.Nald;

        console.log(aggregateId);        
        return [ci];
    }

    let documentCi = function(aggregateId : string) {
        let ci = new ContainedInInformation();
        ci.source = InformationSource.Document;

        console.log(aggregateId);
        return [ci];
    }
    
    return (
        <tr style={{backgroundColor: oddRow ? '#F6F6F6' : '#FAFAFA'}}>
            <td style={{textAlign: 'center'}}>
                <img src={item.thumbnailUrl} width={80} />
            </td>
            <td id={dashesIfNullOrEmpty(item.licenceNumber)}>
                <a href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(item.fileId!, item.licenceId!, item.matchesResultId!);
                   }}
                   dangerouslySetInnerHTML={{ __html: dashesIfNullOrEmpty(item.licenceNumber) }} />
                {item.isLicenceNumberFlagged && <LicenceNumberFlagTag reason={item.licenceNumberFlagReason}/>}
            </td>
            <td className='default-hidden'>{dashesIfNullOrEmpty(item.licenceHolder)}</td>
            <td>{((item.purposes?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.purposes!}/> : '--')}</td>
            <td>{((item.points?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.points!}/> : '--')}</td>
            <td>{dashesIfNullOrZero(item.limitsCount)}</td>
            <td>
                {item.aggregateIds != null && item.aggregateIds!.length > 0 &&
                <ul>
                    {item.aggregateIds.map((aggregateId) => {
                        return (
                            <li>
                                {toShortId(aggregateId)}
                                <NaldTag containedIn={item.naldHasAggregateCondition ? naldCi(aggregateId) : []}/>
                                <DocumentTag containedIn={(item.aggregatesCount ?? 0) > 0 ? documentCi(aggregateId) : []}/>
                            </li>);
                    })}
                </ul>
                }
                {item.aggregateIds === null || item.aggregateIds!.length === 0 &&
                    <span>--</span>
                }
            </td>
            <td>{(item.ocr ? "True" : "False")}</td>
            <td>
                <strong>Doc:</strong><br /> {dashesIfNullOrEmpty(item.issueDate)}
                {item.isIssueDateFlagged && <span title="Issue date mismatch with NALD"> 🚩</span>}
                <br /><strong>Nald:</strong><br /> {dashesIfNullOrEmpty(item.naldSignatureDate)}
                <br /><strong>Nald OG:</strong><br /> {dashesIfNullOrEmpty(item.naldOrigSignatureDate)}
            </td>
            <td>{dashesIfNullOrEmpty(item.issuer)}</td>
            <td>{(item.meansFound ? "True" : "False")}</td>
            <td>
                <LinkedLicencesList
                    item={item}
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
                {sectionsToDisplay.map((sectionName) => {
                    const sectionItems = sectionVerifications
                        .filter(section => section.licenceSectionName === sectionName)
                        .flatMap(section => section.licenceSectionItems ?? []);

                    return (
                            <div key={sectionName} style={{ marginBottom: '10px' }}>
                                <strong>{sectionName}</strong>
                                {sectionItems.length === 0 ? <div>No verifications</div> : <UnorderedListOfStrings items={
                                    [...sectionItems]
                                        .sort((a, b) => compareAlphanumeric(a.licenceSectionItemId, b.licenceSectionItemId))
                                        .map((v) => {
                                            const itemId = v.licenceSectionItemId ?? "Unknown";
                                            return (
                                                <span key={itemId}>
                                                    {itemId}{' '}
                                                    {(v.verificationTypesWithNotes?.length
                                                        ? v.verificationTypesWithNotes
                                                        : v.verificationTypes || []).map((vt: string, idx: number) => (
                                                        <span key={idx} title={getVerificationWithNotesTooltip(vt)} style={{
                                                            backgroundColor: getVerificationTypeBackgroundColor(getVerificationWithNotesFirstPart(vt)),
                                                            color: 'white',
                                                            fontSize: '0.7em',
                                                            padding: '1px 3px',
                                                            borderRadius: '3px',
                                                            marginRight: '2px',
                                                            verticalAlign: 'middle',
                                                            fontWeight: 'bold',
                                                            fontFamily: 'sans-serif'
                                                        }}>
                                                            {getVerificationTypeInitials(getVerificationWithNotesFirstPart(vt))}
                                                        </span>
                                                    ))}
                                                    {v.isFlagged && <span title={v.flagReason || undefined}>🚩</span>}
                                                </span>
                                            );
                                        })
                                }/>}
                            </div>
                    );
                })}
            </td>
        </tr>
    );
}

export default LicencesTableRow;