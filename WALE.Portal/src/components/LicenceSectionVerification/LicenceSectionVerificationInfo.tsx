import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";
import { getVerificationTypeColor } from "../../utils/verificationUtils.ts";

interface LicenceSectionVerificationInfoProps {
    verification: LicenceSectionVerification;
}

export const LicenceSectionVerificationInfo = ({ verification }: LicenceSectionVerificationInfoProps) => {
    return (
        <div style={{ marginRight: 'auto', fontSize: '0.75rem', color: '#666', fontStyle: 'italic' }}>
            <span style={{ 
                fontSize: '0.85rem', 
                color: getVerificationTypeColor(verification.verificationType || ''), 
                fontWeight: 'bold', 
                fontStyle: 'normal' 
            }}>
                {verification.verificationType}
            </span>
            <br/>{verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A'}
            <br/>Process run {verification.processRunId}
            {verification.notes && (
                <>
                    <br/>Notes: {verification.notes}
                </>
            )}
        </div>
    );
};
