import {LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {LicenceSectionVerificationHistory} from "./LicenceSectionVerificationHistory.tsx";
import {LinkedLicenceItem} from "./LinkedLicences/LinkedLicenceItem.tsx";
import {AggregateItem} from "./Aggregates/AggregateItem.tsx";
import type {ComponentType} from "react";

interface LicenceVerificationHistoryProps {
    verifications: LicenceSectionVerification[] | undefined;
    isLoading: boolean;
    onJumpToPage?: (pageNumber: number) => void;
    onRefresh?: () => void;
    onDeleted?: () => void;
}

const SECTION_COMPONENTS: Record<string, ComponentType<any>> = {
    "Linked Licences": LinkedLicenceItem,
    "Aggregates": AggregateItem
};

export function LicenceVerificationHistory({verifications, isLoading, onJumpToPage, onRefresh, onDeleted}: LicenceVerificationHistoryProps) {
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

    const groupKey = (v: LicenceSectionVerification) => `${v.licenceSectionName ?? ''}|${v.licenceSectionItemId ?? ''}`;

    const latestActiveIdByGroup = new Map<string, number>();
    (verifications || [])
        .filter(v => !v.deletedDateTimeUtc)
        .sort((a, b) => {
            const dateA = a.createdDateTimeUtc ? new Date(a.createdDateTimeUtc).getTime() : 0;
            const dateB = b.createdDateTimeUtc ? new Date(b.createdDateTimeUtc).getTime() : 0;
            return dateB - dateA;
        })
        .forEach(v => {
            const key = groupKey(v);
            if (!latestActiveIdByGroup.has(key) && v.licenceSectionVerificationId != null) {
                latestActiveIdByGroup.set(key, v.licenceSectionVerificationId);
            }
        });

    const renderVerificationContent = (verification: LicenceSectionVerification) => {
        const sectionName = verification.licenceSectionName || '';
        const Component = SECTION_COMPONENTS[sectionName];
        const verificationType = verification.verificationType || '';

        const getSnapshotLabel = (type: string) => {
            switch (type) {
                case 'Confirmed':
                case 'AutoConfirm':
                    return 'Confirmed value';
                case 'Edited':
                    return 'Value before this edit';
                case 'Removed':
                    return 'Removed value';
                /* Snapshot should not exist for Added */
            }
        };

        const getOverrideLabel = (type: string) => {
            switch (type) {
                case 'Edited':
                    return 'Value after this edit';
                case 'Added':
                    return 'Added value';
            }
        };

        const renderValue = (value: string | undefined, label?: string) => {
            if (!value) return null;

            let content;
            if (Component) {
                try {
                    const data = JSON.parse(value);
                    content = <Component linkedLicence={data} aggregate={data} isEditing={false} onJumpToPage={onJumpToPage}/>;
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
                {verification.licenceSectionName === 'Linked Licences' && verification.licenceSectionItemId === 'None Outgoing' && (
                    <label>No outgoing linked licences</label>
                )}
                {verification.licenceSectionName === 'Aggregates' && verification.licenceSectionItemId === 'None' && (
                    <label>No aggregates</label>
                )}
                {renderValue(verification.licenceSectionScrapedValue, `Original value (scraped on process run ${verification.processRunId})`)}
                {renderValue(verification.licenceSectionSnapshotValue, getSnapshotLabel(verificationType))}
                {renderValue(verification.licenceSectionOverrideValue, getOverrideLabel(verificationType))}
            </div>
        );
    };

    return (
        <div>
            {sortedVerifications.map((verification, index) => {
                const canDelete = !verification.deletedDateTimeUtc &&
                    latestActiveIdByGroup.get(groupKey(verification)) === verification.licenceSectionVerificationId;

                return (
                    <LicenceSectionVerificationHistory
                        key={verification.licenceSectionVerificationId || index}
                        verification={verification}
                        initialOpen={index === 0}
                        canDelete={canDelete}
                        onRefresh={onRefresh}
                        onDeleted={onDeleted}
                    >
                        {renderVerificationContent(verification)}
                    </LicenceSectionVerificationHistory>
                );
            })}
        </div>
    );
}
