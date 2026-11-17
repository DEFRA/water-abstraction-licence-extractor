import {ContainedInInformation, NullableOfInformationDirection} from "../api/generated/apiClient.ts";

interface OutgoingTagProps {
    containedIn?: ContainedInInformation[];
}

export function OutgoingTag({containedIn}: OutgoingTagProps) {
    const isReferencedAlongside = !!containedIn?.find(ci =>
        ci.direction === NullableOfInformationDirection.Outgoing);
    
    if (!isReferencedAlongside) {
        return null;
    }

    return (
        <span style={{
            backgroundColor: "royalblue",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "2px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>Out</span>
    );
}