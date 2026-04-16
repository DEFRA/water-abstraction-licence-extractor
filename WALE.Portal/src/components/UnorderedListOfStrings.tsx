import type {ReactNode} from "react";

interface UnorderedListOfStringsProps {
    items: (string | ReactNode)[];
}

function UnorderedListOfStrings({items}: UnorderedListOfStringsProps) {
    return (<ul>
        {items.map((item, index) => (
            <li key={index}>{item}</li>
        ))}
    </ul>);
}

export default UnorderedListOfStrings;