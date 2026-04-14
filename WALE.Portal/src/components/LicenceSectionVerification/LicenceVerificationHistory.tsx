import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";
import { LicenceSectionVerificationHistory } from "./LicenceSectionVerificationHistory.tsx";

interface LicenceVerificationHistoryProps {
    verifications: LicenceSectionVerification[] | undefined;
    isLoading: boolean;
}

export function LicenceVerificationHistory({ verifications, isLoading }: LicenceVerificationHistoryProps) {
    if (isLoading) {
        return <div>Loading history...</div>;
    }

    if (!verifications || verifications.length === 0) {
        return <div>No verification history found for this licence.</div>;
    }

    const sortedVerifications = [...(verifications || [])].sort((a, b) => {
        const nameA = a.licenceSectionName || '';
        const nameB = b.licenceSectionName || '';
        if (nameA < nameB) return -1;
        if (nameA > nameB) return 1;

        const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
        const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
        return dateB - dateA;
    });

    return (
        <div>
            {sortedVerifications.map((verification, index) => (
                <LicenceSectionVerificationHistory 
                    key={verification.licenceSectionVerificationId || index} 
                    verification={verification}
                >
                    <div>{verification.licenceSectionValue || 'N/A'}</div>
                </LicenceSectionVerificationHistory>
            ))}
        </div>
    );
}
