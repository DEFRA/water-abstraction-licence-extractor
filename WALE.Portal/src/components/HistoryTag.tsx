import {ContainedInInformation} from "../api/generated/apiClient.ts";

interface HistoryTagProps {
    containedIn?: ContainedInInformation[];
}

export function HistoryTag({containedIn}: HistoryTagProps) {
    const isHistory = !!containedIn?.find(ci =>
        ci.linkReason === "Predecessor" || ci.linkReason === "Successor");
    
    if (!isHistory) {
        return null;
    }

    return (
        <span style={{
            backgroundColor: "beige",
            color: "black",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "2px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>Lineage</span>
    );
}