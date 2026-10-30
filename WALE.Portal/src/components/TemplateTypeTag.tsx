interface TemplateTypeTagProps {
    templateType?: string;
    template?: string;
}

/// The Divisional rule emits the issuing authority's own name, so there is no fixed set to
/// match against.
function backgroundColour(templateType: string) {
    if (templateType.startsWith('EA-Digital')) {
        return 'green';
    }

    if (templateType.startsWith('EA-')) {
        return '#1890ff';
    }

    if (templateType.startsWith('NRA-')) {
        return 'darkorange';
    }

    return '#6a1b9a';
}

export function TemplateTypeTag({templateType, template}: TemplateTypeTagProps) {
    if (!templateType) {
        return null;
    }

    return (
        <span
            title={template ? `Template ${template}` : undefined}
            style={{
                backgroundColor: backgroundColour(templateType),
                color: 'white',
                fontSize: '0.7em',
                padding: '2px 5px',
                borderRadius: '3px',
                fontWeight: 'bold',
                fontFamily: 'sans-serif',
                whiteSpace: 'nowrap'
            }}>
            {templateType}
        </span>
    );
}

export default TemplateTypeTag;
