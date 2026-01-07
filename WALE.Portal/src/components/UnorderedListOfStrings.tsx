interface UnorderedListOfStringsProps {
    items: string[];
}

function UnorderedListOfStrings({items}: UnorderedListOfStringsProps) {
    return (<ul>
        {items.map((item, index) => (
            <li key={index}>{item}</li>
        ))}
    </ul>);
}

export default UnorderedListOfStrings;