import {LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {LicenceSectionVerificationInfo} from "./LicenceSectionVerificationInfo.tsx";

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
            <div style={{flex: 1}}>
                {latestVerification && <LicenceSectionVerificationInfo verification={latestVerification}/>}
            </div>
            <div style={{display: 'flex', flexDirection: 'column', gap: '8px', alignItems: 'flex-end'}}>
                <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                    <button onClick={onVerify} style={{padding: '4px 12px', backgroundColor: '#52c41a', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Confirm</button>
                    <button onClick={onReject} style={{padding: '4px 12px', backgroundColor: '#ff4d4f', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Remove</button>
                    <button onClick={onOverride} style={{padding: '4px 12px', backgroundColor: '#1890ff', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Edit</button>
                </div>
                <div style={{display: 'flex', gap: '8px', alignItems: 'center'}}>
                    <button onClick={onRequestBusinessReview} style={{padding: '4px 12px', backgroundColor: 'darkorange', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Request Business Review</button>
                    <button onClick={onCompleteBusinessReview} style={{padding: '4px 12px', backgroundColor: 'purple', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '0.85rem'}}>Complete Business Review</button>
                </div>
            </div>
        </div>
    );
};
