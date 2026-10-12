interface LicenceNumberFlagTagProps {
    reason?: string;
}

const tagsByReason: Record<string, {label: string, backgroundColor: string}> = {
    "New Licence": {label: "New", backgroundColor: "steelblue"},
    "New Licence Document": {label: "New Doc", backgroundColor: "darkorange"}
};

export function LicenceNumberFlagTag({reason}: LicenceNumberFlagTagProps) {
    const tag = reason ? tagsByReason[reason] : undefined;
    if (!tag) {
        return null;
    }

    return (
        <>
            {" "}
            <span title={reason} style={{
                backgroundColor: tag.backgroundColor,
                color: "white",
                fontSize: "0.7em",
                padding: "2px 5px",
                borderRadius: "3px",
                verticalAlign: "middle",
                fontWeight: "bold",
                fontFamily: "sans-serif",
                whiteSpace: "nowrap"
            }}>
                {tag.label}
            </span>
        </>
    );
}

export default LicenceNumberFlagTag;
