import { useState, useEffect } from "react";
import type {Licence, LicenceSectionVerification} from "../../api/generated/apiClient.ts";
import {LicenceSection} from "./LicenceSection";
import {LinkedLicences} from "./LinkedLicences";
import {LicenceVerificationHistory} from "./LicenceVerificationHistory";
import {waleApiClient} from "../../api/apiClient.ts";

interface VerificationContentProps {
    licence: Licence;
    processRunId: number;
    onJumpToPage: (pageNumber: number) => void;
}

type SubTabType = 'verify' | 'history';

export function VerificationContent({ licence, processRunId, onJumpToPage }: VerificationContentProps) {
    const [activeSubTab, setActiveSubTab] = useState<SubTabType>('verify');
    const [history, setHistory] = useState<LicenceSectionVerification[]>([]);
    const [isLoadingHistory, setIsLoadingHistory] = useState(false);

    useEffect(() => {
        if (activeSubTab === 'history' && licence.dmsFileId) {
            setIsLoadingHistory(true);
            waleApiClient.licenceSectionVerifications(licence.dmsFileId)
                .then((data) => {
                    setHistory(data);
                })
                .catch((error) => {
                    console.error("Error fetching licence history", error);
                })
                .finally(() => {
                    setIsLoadingHistory(false);
                });
        }
    }, [activeSubTab, licence.dmsFileId]);

    return (
        <div id="properties" style={{ padding: '10px' }}>
            <ul className="ul-links">
                <li>
                    <a
                        href="#"
                        className={activeSubTab === 'verify' ? 'selectedTab' : ''}
                        onClick={(e) => {
                            e.preventDefault();
                            setActiveSubTab('verify');
                        }}
                    >
                        Verify
                    </a>
                </li>
                <li>
                    <a
                        href="#"
                        className={activeSubTab === 'history' ? 'selectedTab' : ''}
                        onClick={(e) => {
                            e.preventDefault();
                            setActiveSubTab('history');
                        }}
                    >
                        History
                    </a>
                </li>
            </ul>

            {activeSubTab === 'verify' && (
                <LicenceSection 
                    title="Linked Licences" 
                    licenceFileId={licence.dmsFileId!} 
                    processRunId={processRunId}
                >
                    <LinkedLicences 
                        licence={licence} 
                        isEditing={false} // This will be overridden by LicenceSection's React.cloneElement
                        onJumpToPage={onJumpToPage}
                    />
                </LicenceSection>
            )}

            {activeSubTab === 'history' && (
                <LicenceVerificationHistory 
                    verifications={history}
                    isLoading={isLoadingHistory}
                    onJumpToPage={onJumpToPage}
                />
            )}
        </div>
    );
}
