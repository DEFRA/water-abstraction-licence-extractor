import {JSONPath} from "jsonpath-plus";
import AbstractionLimits from "./AbstractionLimits.tsx";
import type {MatchesResult} from "../api/generated/apiClient.ts";

interface OverviewContentProps {
    reportData: MatchesResult;
    onJumpToPage: (pageNumber: number) => void;
    onOpenLinkedLicence: (filename: string) => void;
}

export function OverviewContent({ reportData, onJumpToPage, onOpenLinkedLicence }: OverviewContentProps) {
    const getText = (dataToUse: any, path: string): string | null => {
        try {
            const results = JSONPath({ path, json: dataToUse });
            if (!results || results.length === 0) return null;
            const matched = results[0];
            if (!matched?.text || matched.text.length === 0) return null;
            return matched.text[0].text;
        } catch {
            return null;
        }
    };

    const getMatches = (dataToUse: any, path: string): any[] => {
        try {
            const results = JSONPath({ path, json: dataToUse });
            return results || [];
        } catch {
            return [];
        }
    };

    const licenceNumber = getText(reportData, '$.matches[?(@.labelGroupName==\'LicenceNumber\')]');
    const licenceHolder = getText(reportData, '$.matches[?(@.labelGroupName==\'Company\')]');
    const purposeMatches = getMatches(reportData, '$.matches[?(@.labelGroupName==\'Purpose\')]');

    return (
        <dl id="properties">
            {licenceNumber && (
                <>
                    <dt>
                        <strong>Licence number</strong>
                    </dt>
                    <dd>{licenceNumber}</dd>
                </>
            )}

            {licenceHolder && (
                <>
                    <dt className="default-hidden">
                        <strong>Licence holder</strong>
                    </dt>
                    <dd className="default-hidden">{licenceHolder}</dd>
                </>
            )}

            <dt>
                <strong>Purpose</strong>
            </dt>
            <dd id="purposes">
                <dl>
                    {purposeMatches.map((purposeMatch, idx) => {
                        const purposeText =
                            purposeMatch.subResults?.[0]?.text?.[0]?.text ||
                            purposeMatch.text?.[0]?.text ||
                            'Unknown purpose';

                        return (
                            <div key={idx}>
                                <dt>
                                    <a
                                        href="#"
                                        onClick={(e) => {
                                            e.preventDefault();
                                            onJumpToPage(purposeMatch.pageNumber);
                                        }}
                                    >
                                        {purposeText}
                                    </a>
                                </dt>
                                <dd>
                                    <AbstractionLimits
                                        reportData={reportData}
                                        onJumpToPage={onJumpToPage}
                                        onOpenLinkedLicence={onOpenLinkedLicence}
                                    />
                                </dd>
                            </div>
                        );
                    })}
                </dl>
            </dd>
        </dl>
    );
}