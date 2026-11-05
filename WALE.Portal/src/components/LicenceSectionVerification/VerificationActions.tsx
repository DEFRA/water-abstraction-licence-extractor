import {LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {LicenceSectionVerificationInfo} from "./LicenceSectionVerificationInfo.tsx";
import {getVerificationTypeBackgroundColor} from "../../utils/verificationUtils.ts";

interface VerificationActionsProps {
    scrapedView?: boolean;
    history?: LicenceSectionVerification[];
    licenceSectionName: string;
    itemId?: string;
    onVerify?: () => void;
    onReject?: () => void;
    onOverride?: () => void;
    onRequestBusinessReview?: () => void;
    onCompleteBusinessReview?: () => void;
}

export const VerificationActions = ({
                                         scrapedView,
                                         history,
                                         licenceSectionName,
                                         itemId,
                                         onVerify,
                                         onReject,
                                         onOverride,
                                         onRequestBusinessReview,
                                         onCompleteBusinessReview
                                     }: VerificationActionsProps) => {
    if (scrapedView || !(onVerify || onReject || onOverride)) {
        return null;
    }

    const latestVerification = (history || [])
        .filter(v => v.licenceSectionName === licenceSectionName && v.licenceSectionItemId === itemId)
        .sort((a, b) => {
            const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
            const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
            return dateB - dateA;
        })[0];

    const latestType = latestVerification?.verificationType;
    const canConfirm = !!itemId && latestType !== 'RequestBusinessReview';
    const canRequestBusinessReview = !!itemId && latestType !== 'RequestBusinessReview';
    const canCompleteBusinessReview = !!itemId && latestType !== 'CompleteBusinessReview';

    return (
        <div style={{
            marginBottom: '10px',
            padding: '10px',
            border: '1px solid #eee',
            borderRadius: '4px',
            backgroundColor: 'white',
            marginTop: '16px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            gap: '16px'
        }}>
            <div style={{flex: 1, minWidth: '50%'}}>
                {latestVerification && <LicenceSectionVerificationInfo verification={latestVerification}/>}
            </div>
            <div style={{display: 'flex', flexDirection: 'column', gap: '8px', alignItems: 'flex-end', maxWidth: '50%', flexShrink: 1}}>
                <div style={{display: 'flex', flexWrap: 'wrap', justifyContent: 'flex-end', gap: '8px', alignItems: 'center'}}>
                    <button disabled={!canConfirm} onClick={onVerify} style={{padding: '4px 12px', backgroundColor: getVerificationTypeBackgroundColor('Confirmed'), color: 'white', border: 'none', borderRadius: '4px', cursor: canConfirm ? 'pointer' : 'not-allowed', opacity: canConfirm ? 1 : 0.5, fontSize: '0.85rem', whiteSpace: 'normal', textAlign: 'center'}}>Confirm</button>
                    <button disabled={!itemId} onClick={onReject} style={{padding: '4px 12px', backgroundColor: getVerificationTypeBackgroundColor('Removed'), color: 'white', border: 'none', borderRadius: '4px', cursor: itemId ? 'pointer' : 'not-allowed', fontSize: '0.85rem', whiteSpace: 'normal', textAlign: 'center'}}>Remove</button>
                    <button onClick={onOverride} style={{padding: '4px 12px', backgroundColor: getVerificationTypeBackgroundColor('Edited'), color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem', whiteSpace: 'normal', textAlign: 'center'}}>Edit</button>
                </div>
                <div style={{display: 'flex', flexWrap: 'wrap', justifyContent: 'flex-end', gap: '8px', alignItems: 'center'}}>
                    <button disabled={!canRequestBusinessReview} onClick={onRequestBusinessReview} style={{padding: '4px 12px', backgroundColor: getVerificationTypeBackgroundColor('RequestBusinessReview'), color: 'white', border: 'none', borderRadius: '4px', cursor: canRequestBusinessReview ? 'pointer' : 'not-allowed', opacity: canRequestBusinessReview ? 1 : 0.5, fontSize: '0.85rem', whiteSpace: 'normal', textAlign: 'center'}}>Request Business Review</button>
                    <button disabled={!canCompleteBusinessReview} onClick={onCompleteBusinessReview} style={{padding: '4px 12px', backgroundColor: getVerificationTypeBackgroundColor('CompleteBusinessReview'), color: 'white', border: 'none', borderRadius: '4px', cursor: canCompleteBusinessReview ? 'pointer' : 'not-allowed', opacity: canCompleteBusinessReview ? 1 : 0.5, fontSize: '0.85rem', whiteSpace: 'normal', textAlign: 'center'}}>Complete Business Review</button>
                </div>
            </div>
        </div>
    );
};
