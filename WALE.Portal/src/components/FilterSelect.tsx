import type {FilterType, OutputListDataItemKey} from "../utils/types.ts";

interface FilterSelectProps {
    id: string;
    field: OutputListDataItemKey;
    type: FilterType;
    subField?: string;
    options: { value: string; label: string }[];
    value: string;
    onChange: (field: string, value: string, type: FilterType, subField?: string) => void;
    onReset?: () => void;
}

export function FilterSelect({
                                 id,
                                 field,
                                 type,
                                 subField,
                                 options,
                                 value,
                                 onChange,
                                 onReset
                             }: FilterSelectProps) {
    const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
        const newValue = e.target.value;

        // Reset other filters when this one changes
        if (onReset && newValue !== 'all' && newValue !== 'All') {
            onReset();
        }

        onChange(field, newValue, type, subField);
    };

    return (
        <select
            id={id}
            value={value}
            onChange={handleChange}
            data-field={field}
            data-type={type}
            data-subfield={subField}
        >
            {options.map(opt => (
                <option key={opt.value} value={opt.value}>
                    {opt.label}
                </option>
            ))}
        </select>
    );
}