import {ContainedInInformation, InformationSource} from "../api/generated/apiClient.ts";

interface NaldOnlyTagProps {
    containedIn?: ContainedInInformation[];
}

export function NaldOnlyTag({containedIn}: NaldOnlyTagProps) {
    if (!containedIn?.some(c => c.source === InformationSource.Nald)){
        return null;
    }
    
    if (containedIn?.some(c => c.source !== InformationSource.Nald)){
        return null;
    }

    return (
        <span style={{
            backgroundColor: "darkorange",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "5px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>
            NALD Only
        </span>
    );
}

export default NaldOnlyTag;
