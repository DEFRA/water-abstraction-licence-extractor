import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";

interface LicenceHistoryProps {
    verifications: LicenceSectionVerification[] | undefined;
    isLoading: boolean;
}

export function LicenceHistory({ verifications, isLoading }: LicenceHistoryProps) {
    if (isLoading) {
        return <div>Loading history...</div>;
    }

    if (!verifications || verifications.length === 0) {
        return <div>No verification history found for this licence.</div>;
    }

    return (
        <div>
            <table className="table">
                <thead>
                    <tr>
                        <th>Date</th>
                        <th>Process Run</th>
                        <th>Action</th>
                        <th>Content</th>
                    </tr>
                </thead>
                <tbody>
                    {verifications.map((verification, index) => (
                        <tr key={verification.id || index}>
                            <td>{verification.createdDateTimeUtc ? new Date(verification.createdDateTimeUtc).toLocaleString() : 'N/A'}</td>
                            <td>{verification.processRunId}</td>
                            <td>{verification.verificationType || 'N/A'}</td>
                            <td>{verification.licenceSectionValue || 'N/A'}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
