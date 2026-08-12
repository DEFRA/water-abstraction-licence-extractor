import React from "react";

interface ValidationErrorProps {
    message?: string;
    style?: React.CSSProperties;
}

export const ValidationError = ({ message, style }: ValidationErrorProps) => {
    if (!message) return null;

    return (
        <div style={{
            color: '#ff4d4f',
            fontSize: '0.75rem',
            marginTop: '2px',
            ...style
        }}>
            {message}
        </div>
    );
};
