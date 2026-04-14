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

    const groupedVerifications = (verifications || []).reduce((acc, verification) => {
        const sectionName = verification.licenceSectionName || 'N/A';
        if (!acc[sectionName]) {
            acc[sectionName] = [];
        }
        acc[sectionName].push(verification);
        return acc;
    }, {} as Record<string, LicenceSectionVerification[]>);

    return (
        <div>
            {Object.entries(groupedVerifications).map(([sectionName, sectionVerifications]) => (
                <LicenceSectionVerificationHistory key={sectionName} title={sectionName}>
                    <table className="table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Process Run</th>
                                <th>Action</th>
                                <th>Value</th>
                            </tr>
                        </thead>
                        <tbody>
                            {sectionVerifications.map((verification, index) => (
                                <tr key={verification.licenceSectionVerificationId || index}>
                                    <td>{verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A'}</td>
                                    <td>{verification.processRunId}</td>
                                    <td>{verification.verificationType || 'N/A'}</td>
                                    <td>{verification.licenceSectionValue || 'N/A'}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </LicenceSectionVerificationHistory>
            ))}
        </div>
    );
}
