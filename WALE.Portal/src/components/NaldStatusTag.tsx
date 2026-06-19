import {NaldLicenceStatus} from "../api/generated/apiClient.ts";

interface NaldStatusTagProps {
    status?: NaldLicenceStatus;
}

export function NaldStatusTag({status}: NaldStatusTagProps) {
    const naldStatus = status ?? NaldLicenceStatus.Unknown;
    
    let backgroundColor = "red";
    if (naldStatus === NaldLicenceStatus.Live) {
        backgroundColor = "green";
    } else if (naldStatus === NaldLicenceStatus.Unknown) {
        backgroundColor = "darkorange";
    }

    return (
        <span style={{
            backgroundColor,
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "5px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>
            {naldStatus}
        </span>
    );
}

export default NaldStatusTag;
