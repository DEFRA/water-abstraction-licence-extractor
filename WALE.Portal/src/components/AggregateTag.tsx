interface AggregateTagProps {
    isBecauseOfAggregate?: boolean;
}

export function AggregateTag({isBecauseOfAggregate}: AggregateTagProps) {
    if (!isBecauseOfAggregate) {
        return null;
    }

    return (
        <span style={{
            backgroundColor: "darkmagenta",
            color: "white",
            fontSize: "0.7em",
            padding: "2px 5px",
            borderRadius: "3px",
            marginLeft: "2px",
            verticalAlign: "middle",
            fontWeight: "bold",
            fontFamily: "sans-serif"
        }}>&#931;</span>
    );
}
