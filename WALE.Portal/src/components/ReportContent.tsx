import {useState, useEffect, useRef} from 'react';
//import {JSONPath} from 'jsonpath-plus';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import '../assets/reportstyles.css';
import {VerificationContent} from "./LicenceSectionVerification/VerificationContent";
import {getImageUrl, getPdfUrl} from "../utils/images.ts";
import {waleApiClient} from '../api/apiClient';
import {Licence, LicenceSet, type MatchesResult, OutputListDataItem} from "../api/generated/apiClient.ts";
import LicenceImages from "./LicenceImages";

interface ReportContentProps {
    fileId: string;
    hideBackLink?: boolean;
    //onOpenLinkedLicence: (fileId: string) => void;
    processRunId: number;
    onRefresh?: () => void;
    outputListDataItem?: OutputListDataItem;
}

type TabType = 'verification' | 'json-new' | 'json-set' | 'json-ai' | 'json' | 'text' | 'images';

export function ReportContent({fileId, hideBackLink = true, /*onOpenLinkedLicence,*/ processRunId, onRefresh, outputListDataItem}: ReportContentProps) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Data states
    const [reportData, setReportData] = useState<MatchesResult | null>(null);
    const [matchesResultString, setMatchesResultString] = useState<string | null>(null);
    const [reportData2, setReportData2] = useState<Licence | null>(null);
    const [licenceSetsData, setLicenceSetsData] = useState<LicenceSet[] | null>(null);
    const [licenceString, setLicenceString] = useState<string | null>(null);
    // const [aiData, setAiData] = useState<AiData | null>(null);
    // const [textData, setTextData] = useState<string>('');

    // UI states
    const [activeTab, setActiveTab] = useState<TabType>('verification');

    const iframeParentRef = useRef<HTMLDivElement>(null);

    // Load all data
    const loadAllData = async () => {
        try {
            setLoading(true);
            
            // Load data using API client
            const [matchesResult, matchesResultString, licenceResult, licenceSetsResult, licenceStringResult] = await Promise.allSettled([
                waleApiClient.matchesResult(fileId),
                waleApiClient.matchesResultString(fileId),
                waleApiClient.licence(fileId, processRunId),
                waleApiClient.licenceSets(fileId),
                waleApiClient.licenceString(fileId, processRunId),
            ]);

            if (matchesResult.status === 'fulfilled') setReportData(matchesResult.value);
            if (matchesResultString.status === 'fulfilled') setMatchesResultString(JSON.parse(matchesResultString.value));
            if (licenceResult.status === 'fulfilled') setReportData2(licenceResult.value);
            if (licenceSetsResult.status === 'fulfilled') setLicenceSetsData(licenceSetsResult.value);
            if (licenceStringResult.status === 'fulfilled') setLicenceString(JSON.parse(licenceStringResult.value));

        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to load report');
            console.error('Error loading report:', err);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadAllData();
    }, [fileId]);

    const handleRefresh = () => {
        loadAllData();
        if (onRefresh) {
            onRefresh();
        }
    };

    // Helper functions (converted from report.js)
    /*const getText = (dataToUse: any, path: string): string | null => {
        const matched = getMatch(dataToUse, path);
        return toText(matched);
    };

    const getMatch = (dataToUse: any, path: string): any => {
        const matches = getMatches(dataToUse, path);
        return matches.length > 0 ? matches[0] : null;
    };

    const getMatches = (dataToUse: any, path: string): any[] => {
        try {
            const results = JSONPath({path, json: dataToUse});
            return results || [];
        } catch {
            return [];
        }
    };

    const toText = (matched: any): string | null => {
        if (!matched?.text || matched.text.length === 0) return null;
        return matched.text[0].text;
    };*/

    const jumpToPage = (pageNumber: number) => {
        const imgEle = document.getElementById(`page${pageNumber}`);
        if (!imgEle || !iframeParentRef.current) return;

        iframeParentRef.current.scrollTo({
            top: imgEle.offsetTop + 350,
            left: 0,
            behavior: 'smooth'
        });
    };

    if (loading) {
        return <div style={{padding: '20px'}}>Loading report...</div>;
    }

    if (error) {
        return <div style={{padding: '20px', color: 'red'}}>Error: {error}</div>;
    }

    if (!reportData) {
        return <div style={{padding: '20px'}}>No report data available</div>;
    }

    return (
        <div id="container">
            <div id="leftCol">
                <div id="left-cell">
                    <div className="leftDetails">
                        {reportData2?.licenceNumber?.value && (
                            <div style={{ marginBottom: '10px', textAlign: 'center' }}>
                                <span style={{ fontWeight: 'bold', fontSize: '1.2em' }}>
                                    Licence Number {reportData2.licenceNumber.value}
                                </span>
                            </div>
                        )}
                        {/* Tab Navigation */}
                        <ul className="ul-links">
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'verification' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('verification');
                                    }}
                                >
                                    Verification
                                </a>
                            </li>
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'json-new' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('json-new');
                                    }}
                                >
                                    Licence data
                                </a>
                            </li>
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'json-set' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('json-set');
                                    }}
                                >
                                    Licence set data
                                </a>
                            </li>
                            <li style={{display: 'none'}}>
                                <a
                                    href="#"
                                    className={activeTab === 'json-ai' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('json-ai');
                                    }}
                                >
                                    JSON (LLM)
                                </a>
                            </li>
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'json' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('json');
                                    }}
                                >
                                    Internal data
                                </a>
                            </li>
                            <li style={{display: 'none'}}>
                                <a
                                    href="#"
                                    className={activeTab === 'text' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('text');
                                    }}
                                >
                                    Digital text
                                </a>
                            </li>
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'images' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('images');
                                    }}
                                >
                                    Images
                                </a>
                            </li>
                        </ul>

                        {/* Tab Content */}
                        {activeTab === 'json-new' && licenceString && (
                            <div id="jsonNewPath">
                                <JsonView src={licenceString} collapsed={1} theme="default"/>
                            </div>
                        )}

                        {activeTab === 'json-set' && licenceSetsData && (
                            <div id="jsonSetPath">
                                <JsonView src={licenceSetsData} collapsed={1} theme="default"/>
                            </div>
                        )}

                        {/*{activeTab === 'json-ai' && (*/}
                        {/*    <div id="jsonAiPath">*/}
                        {/*        {aiData ? (*/}
                        {/*            <JsonView src={aiData} collapsed={1} theme="default" />*/}
                        {/*        ) : (*/}
                        {/*            <pre>AI data not available</pre>*/}
                        {/*        )}*/}
                        {/*    </div>*/}
                        {/*)}*/}

                        {activeTab === 'json' && (
                            <div id="jsonPath">
                                <JsonView src={matchesResultString} collapsed={1} theme="default"/>
                            </div>
                        )}

                        {activeTab === 'text' && (
                            <div id="text">
                                <span
                                    // dangerouslySetInnerHTML={{
                                    //     __html: textData.replaceAll('\n', '<br/>\n')
                                    // }}
                                />
                            </div>
                        )}
                        
                        {activeTab === 'images' && (
                            <div id="images">
                                <LicenceImages fileId={fileId} />
                            </div>
                        )}

                        {activeTab === 'verification' && reportData2 && (
                            <div id="overview">
                                <VerificationContent
                                    licence={reportData2}
                                    processRunId={processRunId}
                                    onJumpToPage={jumpToPage}
                                    onRefresh={handleRefresh}
                                    outputListDataItem={outputListDataItem}
                                />
                            </div>
                        )}

                        {!hideBackLink && (
                            <h1 id="backLink">
                                <a href="/list">Back to all licences</a>
                            </h1>
                        )}
                    </div>
                </div>

                <div id="right-cell">
                    <div id="rightPdf">
                        <h1>
                            <a
                                id="filename"
                                href={`${getPdfUrl(reportData.filename)}`}
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                {reportData.filename}
                            </a>
                        </h1>

                        <div id="iframeParent" ref={iframeParentRef}>
                            <div id="pdf-images">
                                {Array.from({length: reportData.numberOfPages!}, (_, i) => i + 1).map(
                                    (pageNum) => (
                                        <img
                                            key={pageNum}
                                            id={`page${pageNum}`}
                                            src={getImageUrl(`${fileId}`, `${pageNum}`, `PdfPig`)}
                                            alt="JPEG image (text not available)"
                                            onError={(e) => {
                                                e.currentTarget.style.display = 'none';
                                            }}
                                        />
                                    )
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}