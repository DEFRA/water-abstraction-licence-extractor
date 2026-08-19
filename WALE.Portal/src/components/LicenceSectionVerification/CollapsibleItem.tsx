import {type ReactNode, useState} from 'react';

interface CollapsibleItemProps {
    summary: ReactNode;
    defaultOpen?: boolean;
    variant?: 'item' | 'section';
    children: ReactNode;
}

const VARIANT_STYLES: Record<'item' | 'section', {
    border: string;
    headerBg: string;
    bodyBorder: string;
    headerPadding: string;
    bodyPadding: string;
    backgroundColor?: string;
}> = {
    item: {border: '#d9d9d9', headerBg: '#fafafa', bodyBorder: '#eee', headerPadding: '10px 12px', bodyPadding: '12px', backgroundColor: 'white'},
    section: {border: '#aaa', headerBg: '#e8e8e8', bodyBorder: '#aaa', headerPadding: '10px', bodyPadding: '10px'},
};

export function CollapsibleItem({summary, defaultOpen = false, variant = 'item', children}: CollapsibleItemProps) {
    const [isOpen, setIsOpen] = useState(defaultOpen);
    const s = VARIANT_STYLES[variant];

    return (
        <div style={{border: `1px solid ${s.border}`, borderRadius: '4px', marginBottom: '10px', backgroundColor: s.backgroundColor}}>
            <div
                style={{padding: s.headerPadding, backgroundColor: s.headerBg, cursor: 'pointer', display: 'flex', justifyContent: 'space-between', alignItems: 'center'}}
                onClick={() => setIsOpen(!isOpen)}
            >
                <div style={{flex: 1}}>{summary}</div>
                <span style={{marginLeft: '10px'}}>{isOpen ? '▲' : '▼'}</span>
            </div>
            {isOpen && (
                <div style={{padding: s.bodyPadding, borderTop: `1px solid ${s.bodyBorder}`}}>
                    {children}
                </div>
            )}
        </div>
    );
}
