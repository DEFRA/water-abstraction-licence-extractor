import {LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {LicenceSectionVerificationHistory} from "./LicenceSectionVerificationHistory.tsx";
import {LinkedLicenceItem} from "./LinkedLicenceItem.tsx";
import type {ComponentType} from "react";

interface LicenceVerificationHistoryProps {
    verifications: LicenceSectionVerification[] | undefined;
    isLoading: boolean;
    onJumpToPage?: (pageNumber: number) => void;
}

const SECTION_COMPONENTS: Record<string, ComponentType<any>> = {
    "Linked Licences": LinkedLicenceItem
};

export function LicenceVerificationHistory({verifications, isLoading, onJumpToPage}: LicenceVerificationHistoryProps) {
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

        const renderValue = (value: string | undefined, label?: string) => {
            if (!value) return null;

            let content;
            if (Component) {
                try {
                    const data = JSON.parse(value);
                    content = <Component linkedLicence={data} isEditing={false} onJumpToPage={onJumpToPage}/>;
                } catch (e) {
                    console.error("Error parsing verification value", e);
                    content = <div>{value}</div>;
                }
            } else {
                content = <div>{value}</div>;
            }

            return (
                <div key={label} style={label ? {marginBottom: '10px'} : undefined}>
                    {label && <div style={{fontWeight: 'bold', marginBottom: '5px'}}>{label}:</div>}
                    {content}
                </div>
            );
        };
        
        return (
            <div>
                {renderValue(verification.licenceSectionScrapedValue, 'Scraped Value')}
                {renderValue(verification.licenceSectionOverrideValue, 'Override Value')}
            </div>
        );
    };

    return (
        <div>
            {sortedVerifications.map((verification, index) => (
                <LicenceSectionVerificationHistory
                    key={verification.licenceSectionVerificationId || index}
                    verification={verification}
                    initialOpen={index === 0}
                >
                    {renderVerificationContent(verification)}
                </LicenceSectionVerificationHistory>
            ))}
        </div>
    );
}
