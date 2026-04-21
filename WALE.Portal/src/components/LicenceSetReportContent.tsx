import {useEffect, useState} from "react";
import {LicenceSet} from "../api/generated/apiClient.ts";
import waleApiClient from "../api/apiClient.ts";
import JsonView from "react18-json-view";
import {getImageUrl} from "../utils/images.ts";
import "../assets/licencesetstyles.css";

interface LicenceSetReportContentProps {
    fileId: string;
    licenceSetId: string;
    hideBackLink?: boolean;
}

export function LicenceSetReportContent({fileId, licenceSetId, hideBackLink = true}: LicenceSetReportContentProps) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [licenceSetData, setLicenceSetData] = useState<LicenceSet | null>(null);

    useEffect(() => {
        const loadAllData = async () => {
            try {
                setLoading(true);

                // Load data using API client
                const [licenceSetsResult] = await Promise.allSettled([
                    waleApiClient.licenceSets(fileId)
                ]);

                if (licenceSetsResult.status === 'fulfilled') {
                    let licenceSet = licenceSetsResult.value.filter(x => x.licenceSetId === licenceSetId)[0];
                    setLicenceSetData(licenceSet);
                }
            } catch (err) {
                setError(err instanceof Error ? err.message : 'Failed to load report');
                console.error('Error loading report:', err);
            } finally {
                setLoading(false);
            }
        };

        loadAllData();
    }, [fileId, licenceSetId]);

    if (loading) {
        return <div style={{padding: '20px'}}>Loading report...</div>;
    }

    if (error) {
        return <div style={{padding: '20px', color: 'red'}}>Error: {error}</div>;
    }

    if (!licenceSetData) {
        return <div style={{padding: '20px'}}>No licence set data available</div>;
    }

    return (
        <div id="cols" className={`cols-${licenceSetData.licences!.length + 1}`}>
            <div id="col0">
                <h1 id="licence-set-id">{licenceSetId}</h1>

                <JsonView src={licenceSetData} collapsed={1} theme="default"/>

                {!hideBackLink && (
                    <h1 id="backLink">
                        <a href="/list">Back to all licences</a>
                    </h1>
                )}
            </div>
            {licenceSetData.licences!.map((licence, i) => (
                <div key={i}>
                    {licence.filename
                        ? <img src={getImageUrl(`${licence.dmsFileId}/PdfPig/Images/page-1.jpg`)}
                               alt={`Licence sheet 1 for ${licence.filename}`}
                               style={{width: '100%'}}
                        />
                        : <div>--</div>
                    }
                </div>
            ))}
        </div>
    );
}