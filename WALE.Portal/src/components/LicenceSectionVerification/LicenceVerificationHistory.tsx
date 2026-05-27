import { LicenceSectionVerification } from "../../api/generated/apiClient.ts";
import { LicenceSectionVerificationHistory } from "./LicenceSectionVerificationHistory.tsx";
import { LinkedLicenceItem } from "./LinkedLicenceItem.tsx";
import type {ComponentType} from "react";

interface LicenceVerificationHistoryProps {
    verifications: LicenceSectionVerification[] | undefined;
    isLoading: boolean;
    onJumpToPage?: (pageNumber: number) => void;
}

const SECTION_COMPONENTS: Record<string, ComponentType<any>> = {
    "Linked Licences": LinkedLicenceItem
};

export function LicenceVerificationHistory({ verifications, isLoading, onJumpToPage }: LicenceVerificationHistoryProps) {
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

    const renderVerificationContent = (verification: LicenceSectionVerification) => {
        const sectionName = verification.licenceSectionName || '';
        const Component = SECTION_COMPONENTS[sectionName];
        const value = verification.licenceSectionOverrideValue || verification.licenceSectionScrapedValue;

        if (Component && value) {
            try {
                const data = JSON.parse(value);
                return <Component linkedLicence={data} isEditing={false} onJumpToPage={onJumpToPage} />;
            } catch (e) {
                console.error("Error parsing verification value", e);
            }
        }

        return <div>{value || 'N/A'}</div>;
    };

    return (
        <div>
            {sortedVerifications.map((verification, index) => (
                <LicenceSectionVerificationHistory 
                    key={verification.licenceSectionVerificationId || index} 
                    verification={verification}
                >
                    {renderVerificationContent(verification)}
                </LicenceSectionVerificationHistory>
            ))}
        </div>
    );
}
