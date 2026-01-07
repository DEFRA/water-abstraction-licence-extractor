import { useRef, useState, useEffect, type ReactNode } from 'react';

interface DraggableModalProps {
    id: number;
    position: { top: number; left: number };
    size: { width: string; height: string };
    onClose: () => void;
    onMaximize: () => void;
    onMinimize: () => void;
    onPositionChange: (position: { top: number; left: number }) => void;
    children: ReactNode;
}

export function DraggableModal({
                                   id,
                                   position,
                                   size,
                                   onClose,
                                   onMaximize,
                                   onMinimize,
                                   onPositionChange,
                                   children
                               }: DraggableModalProps) {
    const modalRef = useRef<HTMLDivElement>(null);
    const headerRef = useRef<HTMLDivElement>(null);
    const [isDragging, setIsDragging] = useState(false);
    const [dragStart, setDragStart] = useState({ x: 0, y: 0 });

    useEffect(() => {
        if (!isDragging) return;

        const handleMouseMove = (e: MouseEvent) => {
            if (!modalRef.current) return;

            const deltaX = dragStart.x - e.clientX;
            const deltaY = dragStart.y - e.clientY;

            let newTop = position.top - deltaY;
            let newLeft = position.left - deltaX;

            // Prevent dragging off screen
            if (newTop < 0) newTop = 0;
            if (newLeft < 0) newLeft = 0;

            onPositionChange({ top: newTop, left: newLeft });
            setDragStart({ x: e.clientX, y: e.clientY });
        };

        const handleMouseUp = () => {
            setIsDragging(false);
            document.body.classList.remove('dragging');
        };

        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);

        return () => {
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };
    }, [isDragging, dragStart, position, onPositionChange]);

    const handleMouseDown = (e: React.MouseEvent) => {
        if (e.target !== headerRef.current && !headerRef.current?.contains(e.target as Node)) {
            return;
        }

        e.preventDefault();
        setIsDragging(true);
        setDragStart({ x: e.clientX, y: e.clientY });
        document.body.classList.add('dragging');
    };

    return (
        <div
            ref={modalRef}
            className="iframeDiv"
            style={{
                display: 'block',
                position: 'fixed',
                top: `${position.top}px`,
                left: `${position.left}px`,
                width: size.width,
                height: size.height,
                backgroundColor: 'white',
                border: '1px solid #ccc',
                boxShadow: '0 4px 6px rgba(0,0,0,0.1)',
                zIndex: 1000 + id,
                overflow: 'hidden'
            }}
        >
            <div
                ref={headerRef}
                className="iframeDivHeader"
                onMouseDown={handleMouseDown}
                style={{
                    padding: '8px',
                    backgroundColor: '#f0f0f0',
                    borderBottom: '1px solid #ccc',
                    cursor: isDragging ? 'grabbing' : 'grab',
                    display: 'flex',
                    justifyContent: 'flex-end',
                    gap: '8px'
                }}
            >
                <a
                    href="#"
                    onClick={(e) => {
                        e.preventDefault();
                        onMinimize();
                    }}
                    style={{ textDecoration: 'none', fontSize: '16px' }}
                >
                    &#95;
                </a>
                <a
                    href="#"
                    onClick={(e) => {
                        e.preventDefault();
                        onMaximize();
                    }}
                    style={{ textDecoration: 'none', fontSize: '16px' }}
                >
                    &#128470;
                </a>
                <a
                    href="#"
                    onClick={(e) => {
                        e.preventDefault();
                        onClose();
                    }}
                    style={{ textDecoration: 'none', fontSize: '16px' }}
                >
                    &#10006;
                </a>
            </div>
            <div style={{ height: 'calc(100% - 40px)', overflow: 'auto' }}>
                {children}
            </div>
        </div>
    );
}