import {type ContainedInInformation, NullableOfInformationDirection} from "../api/generated/apiClient.ts";

interface ReferencedAlongsideTagProps {
    containedIn?: ContainedInInformation[];
}

export function ReferencedAlongsideTag({containedIn}: ReferencedAlongsideTagProps) {
    const isReferencedAlongside = !!containedIn?.find(ci =>
        ci.direction === NullableOfInformationDirection.ReferencedAlongside);
    
    if (!isReferencedAlongside) {
        return null;
    }

    return (
        <span style={{
            backgroundColor: "yellowgreen",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "2px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>Ref</span>
    );
}