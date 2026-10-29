import {useEffect, useState} from "react";
import {LicenceSet} from "../api/generated/apiClient.ts";
import waleApiClient from "../api/apiClient.ts";
import JsonView from "react18-json-view";
import {getImageUrl} from "../utils/images.ts";
import "../assets/licencesetstyles.css";

interface LicenceSetReportContentProps {
    licenceId: number;
    licenceSetId: string;
    hideBackLink?: boolean;
}

export function LicenceSetReportContent({licenceId, licenceSetId}: LicenceSetReportContentProps) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [licenceSetData, setLicenceSetData] = useState<LicenceSet | null>(null);

    useEffect(() => {
        const loadAllData = async () => {
            try {
                setLoading(true);

                // Load data using API client
                const [licenceSetsResult] = await Promise.allSettled([
                    waleApiClient.licenceSetsByLicenceIdString(licenceId)
                ]);

                if (licenceSetsResult.status === 'fulfilled') {
                    let licenceSets= JSON.parse(licenceSetsResult.value) as LicenceSet[];
                    let licenceSet = licenceSets.filter(ls => ls.licenceSetId === licenceSetId)[0];
                    
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
    }, [licenceId, licenceSetId]);

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
                <div style={{padding: '20px'}}>
                    <h1 id="licence-set-id">{licenceSetId}</h1>
    
                    <div style={{marginTop: '20px'}}>
                        <JsonView src={licenceSetData} collapsed={1} theme="default"/>
                    </div>
                </div>                    
            </div>
            {licenceSetData.licences!.map((licence, i) => (
                <div key={i}>
                    {licence.filename
                        ? <img src={getImageUrl(`${licence.dmsFileId}`, `1`, `PdfPig`)}
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