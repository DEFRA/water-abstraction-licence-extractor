import {ContainedInInformation} from "../../api/generated/apiClient.ts";

interface ContainedInListProps {
    sections: ContainedInInformation[];
    onJumpToPage?: (pageNumber: number) => void;
    showLinkReason?: boolean;
}

export const ContainedInList = ({sections, onJumpToPage, showLinkReason}: ContainedInListProps) => {
    if (!sections || sections.length === 0) {
        return null;
    }

    return (
        <div style={{marginTop: '12px', fontSize: '0.9rem'}}>
            <strong style={{display: 'block', marginBottom: '8px'}}>Contained In:</strong>
            <ul style={{margin: 0, padding: 0, listStyle: 'none'}}>
                {sections.map((section, idx) => (
                    <li key={idx} style={{marginBottom: '8px', padding: '8px', backgroundColor: '#f9f9f9', borderRadius: '4px'}}>
                        <div style={{display: 'flex', flexWrap: 'wrap', gap: '8px 16px', alignItems: 'center'}}>
                            <div><strong>Source:</strong> {section.source || 'N/A'}</div>
                            <div><strong>Section:</strong> {section.sectionName || 'N/A'}</div>
                            {showLinkReason && (
                                <div><strong>Link Reason:</strong> {section.linkReason || 'N/A'}</div>
                            )}
                            {section.pageNumber !== undefined && section.pageNumber !== null && section.pageNumber > 0 && (
                                <button
                                    onClick={() => onJumpToPage && onJumpToPage(section.pageNumber!)}
                                    title={`Jump to page ${section.pageNumber}`}
                                    style={{
                                        background: 'none', border: '1px solid #d9d9d9', borderRadius: '4px',
                                        cursor: 'pointer', fontSize: '0.85rem', padding: '2px 6px',
                                        display: 'flex', alignItems: 'center', gap: '4px'
                                    }}
                                >
                                    📄 <span style={{fontSize: '0.75rem'}}>Page {section.pageNumber}</span>
                                </button>
                            )}
                        </div>
                    </li>
                ))}
            </ul>
        </div>
    );
};
