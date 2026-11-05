import { useState} from 'react';
import { waleApiBaseUrl } from '../api/apiClient.ts';

export function LicenceFinderAction() {

    const [error, setError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);
    const [isStarting, setIsStarting] = useState(false);
    const [targetFlowToRun, setTargetFlowToRun] = useState<string | undefined>();


    const startFindLicenceProcess = async () => {
        if (!targetFlowToRun) {
            setError("Please select a flow to run.");
            return;
        }

        setError(null);
        setSuccessMessage(null);
        setIsStarting(true);

        try {
            const response = await fetch(
                `${waleApiBaseUrl}/BFF/Message/SendLicenceFinderProcessMessage` +
                `?FlowToRun=${targetFlowToRun}`,
                {
                    method: "POST"
                }
            );

            if (!response.ok) {
                throw new Error(await response.text());
            }

            setSuccessMessage(
                "Licence finder process started successfully."
            );
        } catch (error) {
            const message =
                error instanceof Error
                    ? error.message
                    : "Find Licence Process Error";

            setError(message);
        } finally {
            setIsStarting(false);
        }
    };

    return (
        <>

            <div className="container" style={{
                border: '1px solid #c3e6cb',
                color: '#155724',
                padding: '10px',
                marginBottom: '10px',
                marginTop: '40px',
                borderRadius: '4px',
                position: 'relative'
            }} >


                <h3>Licence Finder Actions</h3>
                <div style={{ marginBottom: "12px" }}>
                    <label
                        htmlFor="targetProcessRunId"
                        style={{
                            display: "block",
                            marginBottom: "4px",
                            fontWeight: 600
                        }}
                    >
                        Flow To Run
                    </label>

                    <select style={{
                        width: '200px'
                    }}
                            id="targetFlowToRun"
                            value={targetFlowToRun ?? ""}
                            onChange={e =>
                                setTargetFlowToRun(
                                    e.target.value === ""
                                        ? undefined
                                        :e.target.value
                                )
                            }
                    >
                        <option value="">Select file action</option>
                        <option value="FindLicenceFilesAsync">Find Licence Files</option>
                        <option value="FindAllFilesToDownload">Find All Files To Download</option>
                        <option value="FindInspectionReportFiles">Find Inspection Report Files</option>
                        <option value="BuildFileTemplateIdentificationExtract">Build File Template Identification Extract</option>
                        <option value="FindLicenceFilesToDownload">Find Licence Files To Download</option>
                        <option value="FindLicenceFilesToDownload_SpreadsheetCompareOnly">Find Licence Files To Download Spreadsheet Compare Only</option>
                        <option value="FindDuplicateLicenceFiles">Find Duplicate Licence Files</option>
                    </select>
                    <button
                        onClick={startFindLicenceProcess}
                        disabled={isStarting}
                        style={{
                            backgroundColor: '#dc3545',
                            color: 'white',
                            border: 'none',
                            padding: '5px 10px',
                            borderRadius: '4px',
                            cursor: isStarting ? 'not-allowed' : 'pointer'
                        }}
                    >
                        {isStarting ? 'Starting...' : 'Start Licence Finder Process'}
                    </button>

                    {error && (
                        <div
                            style={{
                                backgroundColor: 'pink',
                                border: '1px solid #c3e6cb',
                                color: '#155724',
                                padding: '10px',
                                marginBottom: '10px',
                                borderRadius: '4px',
                                position: 'relative'
                            }}
                        >
                            <p style={{ margin: 0 }}>{error}</p>
                            <button
                                onClick={() => setError(null)}
                                style={{
                                    position: 'absolute',
                                    top: '5px',
                                    right: '10px',
                                    border: 'none',
                                    background: 'transparent',
                                    color: '#721c24',
                                    fontSize: '20px',
                                    cursor: 'pointer',
                                    fontWeight: 'bold'
                                }}
                            >
                                X
                            </button>
                        </div>
                    )}

                    {successMessage && (
                        <div
                            style={{
                                backgroundColor: '#d4edda',
                                border: '1px solid #c3e6cb',
                                color: '#155724',
                                padding: '10px',
                                marginBottom: '10px',
                                borderRadius: '4px',
                                position: 'relative'
                            }}
                        >
                            <p style={{ margin: 0 }}>{successMessage}</p>
                            <button
                                onClick={() => setSuccessMessage(null)}
                                style={{
                                    position: 'absolute',
                                    top: '5px',
                                    right: '10px',
                                    border: 'none',
                                    background: 'transparent',
                                    color: '#155724',
                                    fontSize: '20px',
                                    cursor: 'pointer',
                                    fontWeight: 'bold'
                                }}
                            >
                                ×
                            </button>
                        </div>
                    )}
                    
                </div>
                
            </div>
        </>
    );
}

export default LicenceFinderAction;