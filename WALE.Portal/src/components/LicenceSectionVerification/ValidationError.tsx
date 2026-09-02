import React from "react";

interface ValidationErrorProps {
    message?: string;
    style?: React.CSSProperties;
}

export const ValidationError = ({ message, style }: ValidationErrorProps) => {
    // Always render (reserving a fixed line of height) rather than returning null when there's no
    // message — sibling fields in the same flex/grid row are bottom-aligned, so a field that never
    // shows an error would otherwise collapse and throw the whole row out of line as soon as another
    // field in that row displays one.
    return (
        <div style={{
            color: '#ff4d4f',
            fontSize: '0.75rem',
            marginTop: '2px',
            minHeight: '14px',
            lineHeight: '14px',
            ...style
        }}>
            {message || ' '}
        </div>
    );
};
