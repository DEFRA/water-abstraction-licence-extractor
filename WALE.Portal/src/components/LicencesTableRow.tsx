import {OutputListDataItem} from "../api/generated/apiClient.ts";
import {
    getVerificationTypeBackgroundColor,
    getVerificationTypeInitials,
    getVerificationWithNotesFirstPart,
    getVerificationWithNotesTooltip
} from "../utils/verificationUtils.ts";
import {compareAlphanumeric, dashesIfNullOrEmpty, dashesIfNullOrZero} from "../utils/formatting.ts";
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
    expanded?: boolean;
    onToggleExpand?: () => void;
    onLookUp?: () => void;
    lookUpHref?: string;
    linkToEstate?: boolean;
    onOpenReport: (fileId: string, licenceId: number, matchesResultId: number) => void;
    onOpenLicenceSetReport: (fileId: string, licenceId: number, matchesResultId: number, licenceSetId: string) => void;
    showSingles: boolean;
    verificationSections?: string[];
}

function LicencesTableRow({item, oddRow, expanded, onToggleExpand, onLookUp, lookUpHref, linkToEstate, onOpenReport, onOpenLicenceSetReport, showSingles, verificationSections}: OutputItemTableRowProps) {
    const sectionVerifications = item.licenceSectionVerifications ?? [];
    const sectionsToDisplay = getVerificationSectionsToDisplay(
        sectionVerifications.map(section => section.licenceSectionName),
        verificationSections);

    let toShortId = function(id: string | undefined){
        const parts = (id ?? "").split("-");
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
                
                if (shortPart.startsWith('R')) {
                    shortPart = part.substring(part.length - 6);                    
                } else if (shortPart.startsWith('A')
                    || shortPart.startsWith('B')
                    || shortPart.startsWith('G')
                    || shortPart.startsWith('S')) {
                    shortPart = part.substring(part.length - 4);
                }
            }
            
            sb.push(shortPart);
        }
        
        return sb.join('-');
    }
        
    return (
        <tr style={{backgroundColor: oddRow ? '#F6F6F6' : '#FAFAFA'}}>
            <td style={{textAlign: 'center', position: 'relative'}}>
                <img src={item.thumbnailUrl} width={80} />
                {onToggleExpand && (
                    <button
                        onClick={onToggleExpand}
                        title={expanded ? 'Hide earlier runs' : 'Show every run'}
                        style={{position: 'absolute', bottom: '4px', left: '4px', cursor: 'pointer'}}>
                        {expanded ? '\u25BE' : '\u25B8'}
                        {(item.documentCount ?? 1) > 1 && ` ${item.documentCount} docs`}
                    </button>
                )}
            </td>
            <td id={dashesIfNullOrEmpty(item.licenceNumber)} style={{position: 'relative'}}>
                <a href="#"
                   onClick={(e) => {
                       e.preventDefault();
                       onOpenReport(item.fileId!, item.licenceId!, item.matchesResultId!);
                   }}
                   dangerouslySetInnerHTML={{ __html: dashesIfNullOrEmpty(item.licenceNumber) }} />
                {item.isLicenceNumberFlagged && <LicenceNumberFlagTag reason={item.licenceNumberFlagReason}/>}
                {onLookUp && (
                    <a href={lookUpHref ?? '#'}
                       title="Everything held for this licence - open in a new tab to keep the list"
                       style={{position: 'absolute', bottom: '4px', left: '4px'}}
                       onClick={(e) => {
                           if (e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0) {
                               return;
                           }

                           e.preventDefault();
                           onLookUp();
                       }}>🗂 licence record</a>
                )}
            </td>
            <td className='default-hidden'>{dashesIfNullOrEmpty(item.licenceHolder)}</td>
            <td>{((item.purposes?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.purposes!}/> : '--')}</td>
            <td>{((item.points?.length ?? 0) > 0 ? <UnorderedListOfStrings items={item.points!}/> : '--')}</td>
            <td>{dashesIfNullOrZero(item.limitsCount)}</td>
            <td>
                {item.aggregates != null && item.aggregates!.length > 0 &&
                    <ul>
                        {item.aggregates.map((aggregate) => {
                            return (
                                <li>
                                    {toShortId(aggregate.id)}
                                    <NaldTag containedIn={aggregate.containedIn}/>
                                    <DocumentTag containedIn={aggregate.containedIn}/>
                                </li>);
                        })}
                    </ul>
                }
                {(item.aggregates === null || item.aggregates!.length === 0) &&
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
                    linkToEstate={linkToEstate}
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