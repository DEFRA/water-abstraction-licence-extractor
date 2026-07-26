import { LicenceSectionVerification, LicenceSectionItemSummary } from "../../api/generated/apiClient.ts";
import { getVerificationTypeColor } from "../../utils/verificationUtils.ts";

interface LicenceSectionVerificationInfoProps {
    verification: LicenceSectionVerification | LicenceSectionItemSummary;
}

export const LicenceSectionVerificationInfo = ({ verification }: LicenceSectionVerificationInfoProps) => {
    const isVerification = 'verificationType' in verification;
    const verificationType = isVerification ? verification.verificationType : verification.verificationTypes?.[0];
    const createdDateTimeUtc = isVerification ? verification.createdDateTimeUtc : undefined;
    const processRunId = isVerification ? verification.processRunId : undefined;
    const notes = isVerification ? verification.notes : undefined;

    return (
        <div style={{ marginRight: 'auto', fontSize: '0.75rem', color: '#666', fontStyle: 'italic' }}>
            <span style={{ 
                fontSize: '0.85rem', 
                color: getVerificationTypeColor(verificationType || ''), 
                fontWeight: 'bold', 
                fontStyle: 'normal' 
            }}>
                {verificationType}
            </span>
            {createdDateTimeUtc && <><br/>{new Date(createdDateTimeUtc).toLocaleString()}</>}
            {processRunId && <><br/>Process run {processRunId}</>}
            {notes && (
                <>
                    <br/>Notes: {notes}
                </>
            )}
        </div>
    );
};
