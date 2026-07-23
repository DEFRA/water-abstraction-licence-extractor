import { useState, useEffect, useCallback } from "react";
import type {Licence, LicenceSectionVerification, OutputListDataItem} from "../../api/generated/apiClient.ts";
import {LicenceSection} from "./LicenceSection";
import {ScrapedLicenceSection} from "./ScrapedLicenceSection";
import {LinkedLicences} from "./LinkedLicences";
import {LicenceVerificationHistory} from "./LicenceVerificationHistory";
import {waleApiClient} from "../../api/apiClient.ts";

interface VerificationContentProps {
    licence: Licence;
    processRunId: number;
    onJumpToPage: (pageNumber: number) => void;
    onRefresh?: () => void;
    outputListDataItem?: OutputListDataItem;
}

type SubTabType = 'scraped' | 'current' | 'history';

export function VerificationContent({ licence, processRunId, onJumpToPage, onRefresh, outputListDataItem }: VerificationContentProps) {
    const [activeSubTab, setActiveSubTab] = useState<SubTabType>('current');
    const [history, setHistory] = useState<LicenceSectionVerification[]>([]);
    const [isLoadingHistory, setIsLoadingHistory] = useState(false);
    const [scrapedResetKey, setScrapedResetKey] = useState(0);

    const fetchHistory = useCallback(() => {
        if (licence.dmsFileId) {
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
    }, [licence.dmsFileId]);

    const handleVerified = () => {
        fetchHistory();
        setScrapedResetKey(prev => prev + 1);
    };

    useEffect(() => {
        if (activeSubTab === 'history') {
            fetchHistory();
        }
    }, [activeSubTab, fetchHistory]);

    return (
        <div id="properties" style={{ padding: '10px' }}>
            <ul className="ul-links">
                <li>
                    <a
                        href="#"
                        className={activeSubTab === 'current' ? 'selectedTab' : ''}
                        onClick={(e) => {
                            e.preventDefault();
                            setActiveSubTab('current');
                        }}
                    >
                        Current
                    </a>
                </li>
                <li>
                    <a
                        href="#"
                        className={activeSubTab === 'scraped' ? 'selectedTab' : ''}
                        onClick={(e) => {
                            e.preventDefault();
                            setActiveSubTab('scraped');
                        }}
                    >
                        Scraped / Original
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

            {activeSubTab === 'current' && (
                <div key={scrapedResetKey}>
                    <LicenceSection 
                        title="Linked Licences" 
                        itemType="linked licence"
                        licenceFileId={licence.dmsFileId!} 
                        processRunId={processRunId}
                        onRefresh={onRefresh}
                        onVerified={handleVerified}
                        initialOpen={true}
                        outputListDataItem={outputListDataItem}
                    >
                        <LinkedLicences 
                            licence={licence} 
                            onJumpToPage={onJumpToPage}
                            outputListDataItem={outputListDataItem}
                        />
                    </LicenceSection>
                </div>
            )}

            {activeSubTab === 'scraped' && (
                <div>
                    <ScrapedLicenceSection 
                        title="Linked Licences" 
                        itemType="linked licence"
                        licenceFileId={licence.dmsFileId!} 
                        processRunId={processRunId}
                        initialOpen={true}
                        outputListDataItem={outputListDataItem}
                    >
                        <LinkedLicences 
                            licence={licence} 
                            onJumpToPage={onJumpToPage}
                            outputListDataItem={outputListDataItem}
                            scrapedView={true}
                        />
                    </ScrapedLicenceSection>
                </div>
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
