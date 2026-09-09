interface ImpoundmentTagProps {
    licenceType?: string;
}

export function ImpoundmentTag({licenceType}: ImpoundmentTagProps) {
    const impoundmentTag = licenceType == 'Impoundment' ? 'IMP' : '';
    
    let backgroundColor = "darkorange";

    if (impoundmentTag ==='')
    {
        return ;
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
            {impoundmentTag}
        </span>
    );
}

export default ImpoundmentTag;
