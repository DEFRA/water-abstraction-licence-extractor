import {useState, useEffect, useRef} from 'react';
import {JSONPath} from 'jsonpath-plus';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import '../assets/reportstyles.css';
import {VerificationContent} from "./VerificationContent.tsx";
import {getImageUrl} from "../utils/images.ts";
import {waleApiClient} from '../api/apiClient';
import {Licence, LicenceSet, type MatchesResult} from "../api/generated/apiClient.ts";
import LicenceImages from "./LicenceImages.tsx";

interface ReportContentProps {
    filename: string;
    hideBackLink?: boolean;
    onOpenLinkedLicence: (filename: string) => void;
}

type TabType = 'verification' | 'json-new' | 'json-set' | 'json-ai' | 'json' | 'text' | 'images';
type ViewType = 1 | 2;

export function ReportContent({filename, hideBackLink = true, onOpenLinkedLicence}: ReportContentProps) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    // Data states
    const [reportData, setReportData] = useState<MatchesResult | null>(null);
    const [reportData2, setReportData2] = useState<Licence | null>(null);
    const [licenceSetsData, setLicenceSetsData] = useState<LicenceSet[] | null>(null);
    // const [aiData, setAiData] = useState<AiData | null>(null);
    // const [textData, setTextData] = useState<string>('');

    // UI states
    const [activeTab, setActiveTab] = useState<TabType>('verification');
    const [activeView, setActiveView] = useState<ViewType>(1);

    // Form states
    const [licenceNumber, setLicenceNumber] = useState('');
    const [licenceHolder, setLicenceHolder] = useState('');
    const [showLicenceHolder, setShowLicenceHolder] = useState(false);

    const iframeParentRef = useRef<HTMLDivElement>(null);

    // Load all data
    useEffect(() => {
        const loadAllData = async () => {
            try {
                setLoading(true);

                // Load data using API client
                const [matchesResult, licenceResult, licenceSetsResult] = await Promise.allSettled([
                    waleApiClient.matchesResult(filename),
                    waleApiClient.licence(filename),
                    waleApiClient.licenceSets(filename)
                ]);

                if (matchesResult.status === 'fulfilled') setReportData(matchesResult.value);
                if (licenceResult.status === 'fulfilled') setReportData2(licenceResult.value);
                if (licenceSetsResult.status === 'fulfilled') setLicenceSetsData(licenceSetsResult.value);

                // Extract form values from data
                if (matchesResult.status === 'fulfilled') {
                    const licNum = getText(matchesResult.value, '$.matches[?(@.labelGroupName==\'LicenceNumber\')]');
                    const licHolder = getText(matchesResult.value, '$.matches[?(@.labelGroupName==\'Company\')]');

                    if (licNum) setLicenceNumber(licNum);
                    if (licHolder) setLicenceHolder(licHolder);
                }

            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to load report');
                console.error('Error loading report:', err);
            } finally {
                setLoading(false);
            }
        };

        loadAllData();
    }, [filename]);

    // Helper functions (converted from report.js)
    const getText = (dataToUse: any, path: string): string | null => {
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
    };

    const jumpToPage = (pageNumber: number) => {
        const imgEle = document.getElementById(`page${pageNumber}`);
        if (!imgEle || !iframeParentRef.current) return;

        iframeParentRef.current.scrollTo({
            top: imgEle.offsetTop + 350,
            left: 0,
            behavior: 'smooth'
        });
    };

    const handleContinue = () => {
        setShowLicenceHolder(true);
    };

    const handleBack = () => {
        setShowLicenceHolder(false);
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
                        {/* View/Edit Toggle */}
                        <div id="view-edit">
                            <a
                                href="#"
                                className={activeView === 1 ? 'viewSelected' : ''}
                                onClick={(e) => {
                                    e.preventDefault();
                                    setActiveView(1);
                                }}
                            >
                                View
                            </a>
                            {' | '}
                            <a
                                href="#"
                                className={activeView === 2 ? 'viewSelected' : ''}
                                onClick={(e) => {
                                    e.preventDefault();
                                    setActiveView(2);
                                }}
                            >
                                Edit
                            </a>
                        </div>

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
                                    JSON
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
                                    JSON (set)
                                </a>
                            </li>
                            <li>
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
                                    JSON (int.)
                                </a>
                            </li>
                            <li>
                                <a
                                    href="#"
                                    className={activeTab === 'text' ? 'selectedTab' : ''}
                                    onClick={(e) => {
                                        e.preventDefault();
                                        setActiveTab('text');
                                    }}
                                >
                                    Text
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
                        {activeTab === 'json-new' && reportData2 && (
                            <div id="jsonNewPath">
                                <JsonView src={reportData2} collapsed={1} theme="default"/>
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
                                <JsonView src={reportData} collapsed={1} theme="default"/>
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
                                <LicenceImages filename={filename} />
                            </div>
                        )}

                        {activeTab === 'verification' && activeView === 1 && reportData2 && (
                            <div id="overview">
                                <VerificationContent
                                    licence={reportData2}
                                />
                            </div>
                        )}

                        {activeTab === 'verification' && activeView === 2 && (
                            <div id="propertiesNew">
                                <div
                                    id="licenceNumberTxtDiv"
                                    style={{visibility: showLicenceHolder ? 'hidden' : 'visible'}}
                                >
                                    <label htmlFor="licenceNumberTxt">Licence number</label>
                                    <br/>
                                    <input
                                        type="text"
                                        id="licenceNumberTxt"
                                        value={licenceNumber}
                                        onChange={(e) => setLicenceNumber(e.target.value)}
                                    />
                                </div>

                                <div
                                    id="licenceHolderTxtDiv"
                                    className="default-hidden"
                                    style={{visibility: showLicenceHolder ? 'visible' : 'hidden'}}
                                >
                                    <label htmlFor="licenceHolderTxt">Licence holder</label>
                                    <br/>
                                    <input
                                        type="text"
                                        id="licenceHolderTxt"
                                        value={licenceHolder}
                                        onChange={(e) => setLicenceHolder(e.target.value)}
                                    />
                                </div>

                                <div id="back-continue-area">
                                    {showLicenceHolder && (
                                        <button id="backButton" onClick={handleBack}>
                                            Back
                                        </button>
                                    )}
                                    <button id="continueButton" onClick={handleContinue}>
                                        Continue
                                    </button>
                                </div>
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
                                href={`/pdfs/${reportData.filename}`}
                                target="_blank"
                                rel="noopener noreferrer"
                            >
                                {filename}
                            </a>
                        </h1>

                        <div id="iframeParent" ref={iframeParentRef}>
                            <div id="pdf-images">
                                {Array.from({length: reportData.numberOfPages!}, (_, i) => i + 1).map(
                                    (pageNum) => (
                                        <img
                                            key={pageNum}
                                            id={`page${pageNum}`}
                                            src={getImageUrl(`${filename}/PdfPig/Images/page-${pageNum}.jpg`)}
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