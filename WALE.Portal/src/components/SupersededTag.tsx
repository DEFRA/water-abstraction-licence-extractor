import {LinkedLicence} from "../api/generated/apiClient.ts";

interface SupersededTagProps {
    superseded: LinkedLicence[];
}

export function SupersededTag({superseded}: SupersededTagProps) {
    if (!superseded.length) {
        return null;
    }

    const numbers = superseded.map(linkedLicence => linkedLicence.licenceNumber).join(", ");

    return (
        <span title={`Superseded: ${numbers}`}
              style={{
                  backgroundColor: "#e8e0f0",
                  color: "black",
                  fontSize: "0.7em",
                  padding: "2px 5px",
                  borderRadius: "3px",
                  marginLeft: "2px",
                  verticalAlign: "middle",
                  fontWeight: "bold",
                  fontFamily: "sans-serif"
              }}>+{superseded.length} earlier</span>
    );
}

export default SupersededTag;
