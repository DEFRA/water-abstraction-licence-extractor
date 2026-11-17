import {ContainedInInformation, InformationSource} from "../api/generated/apiClient.ts";

interface DocumentTagProps {
    containedIn?: ContainedInInformation[];
}

export function DocumentTag({containedIn}: DocumentTagProps) {
    if (!containedIn?.some(c => c.source === InformationSource.Document)){
        return null;
    }
    
    return (
        <span style={{
            backgroundColor: "lightcoral",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "5px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>
            Document
        </span>
    );
}

export default DocumentTag;