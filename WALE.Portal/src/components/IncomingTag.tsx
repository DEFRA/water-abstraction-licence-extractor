import {ContainedInInformation, NullableOfInformationDirection} from "../api/generated/apiClient.ts";

interface IncomingTagProps {
    containedIn?: ContainedInInformation[];
}

export function IncomingTag({containedIn}: IncomingTagProps) {
    const isIncoming = !!containedIn?.find(ci =>
        ci.direction === NullableOfInformationDirection.Incoming);
    
    if (!isIncoming) {
        return null;
    }

    return (
        <span style={{
            backgroundColor: "hotpink",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "2px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>In</span>
    );
}