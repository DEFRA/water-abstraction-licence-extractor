import type {Licence, OutputListDataItem} from "../../api/generated/apiClient.ts";
import {CollapsibleItem} from "./CollapsibleItem.tsx";

interface DocumentInfoProps {
    licence: Licence;
    outputListDataItem?: OutputListDataItem;
}

export function DocumentInfo({ licence, outputListDataItem }: DocumentInfoProps) {
    return (
        <CollapsibleItem
            variant="section"
            defaultOpen={true}
            summary={<h3 style={{ margin: 0, fontSize: '1.1rem' }}>Document Info</h3>}
        >
            <div id="simpleOverview" style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'flex-start', columnGap: '1.5em' }}>
                <span style={{ whiteSpace: 'nowrap' }}>
                    <strong>Licence has Nald Aggs:</strong> {licence.naldHasAggregateCondition ?? false ? "True" : "False"}
                </span>
                <span style={{ whiteSpace: 'nowrap' }}>
                    <strong>Doc Issue:</strong> {licence.licenceVersion?.issueDate ? new Date(licence.licenceVersion.issueDate).toLocaleDateString() : 'N/A'}
                    {outputListDataItem?.isIssueDateFlagged && <span title="Issue date mismatch with NALD"> 🚩</span>}
                </span>
                <span style={{ whiteSpace: 'nowrap' }}>
                    <strong>Nald Sig:</strong> {licence.licenceVersion?.naldSignatureDate ? new Date(licence.licenceVersion.naldSignatureDate).toLocaleDateString() : 'N/A'}
                </span>
                <span style={{ whiteSpace: 'nowrap' }}>
                    <strong>Nald OG Sig:</strong> {licence.licenceVersion?.naldOrigSignatureDate ? new Date(licence.licenceVersion.naldOrigSignatureDate).toLocaleDateString() : 'N/A'}
                </span>
            </div>
        </CollapsibleItem>
    );
}
