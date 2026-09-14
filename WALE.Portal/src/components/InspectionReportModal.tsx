import {useState, useEffect, useMemo, useRef} from 'react';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import {waleApiClient, waleApiBaseUrl} from '../api/apiClient';
import {getImageUrl, getPdfUrl} from '../utils/images.ts';
import type {MatchesResult} from '../api/generated/apiClient.ts';
import {DraggableModal} from './DraggableModal';

interface InspectionReportModalProps {
    fileId: string;
    processRunId: number;
    onClose: () => void;
}

interface FieldBox {
    pageNumber: number;
    left: number;
    right: number;
    top: number;
    bottom: number;
}

// One box per matched line, in PDF point-space (same units as DocumentLineWordCoordinates) -
// deliberately built from the WORD coordinates within each matched line, not the line's own
// top/left/right/bottom, since the line-level box covers the whole row (label text included);
// the words are what's left after the label/column narrowing, i.e. just the extracted value.
function buildFieldBoxes(matches: unknown): Map<string, FieldBox[]> {
    const map = new Map<string, FieldBox[]>();

    const visit = (groups: unknown) => {
        if (!Array.isArray(groups)) return;

        for (const group of groups as Record<string, any>[]) {
            const name = group.matchedLabelName as string | undefined;
            const lines = group.text as Record<string, any>[] | undefined;

            if (name && lines?.length && !map.has(name)) {
                const boxes: FieldBox[] = [];

                for (const line of lines) {
                    const words = (line.columns ?? []).flatMap((c: any) => c.words ?? []);
                    const coords = words
                        .map((w: any) => w.coordinates)
                        .filter((c: any) => c && c.left >= 0 && c.right >= 0);

                    if (coords.length === 0) continue;

                    boxes.push({
                        pageNumber: line.pageNumber,
                        left: Math.min(...coords.map((c: any) => c.left)),
                        right: Math.max(...coords.map((c: any) => c.right)),
                        top: Math.max(...coords.map((c: any) => c.top)),
                        bottom: Math.min(...coords.map((c: any) => c.bottom)),
                    });
                }

                if (boxes.length > 0) map.set(name, boxes);
            }

            if (group.subResults?.length) visit(group.subResults);
        }
    };

    visit(matches);
    return map;
}

// PDF points use a bottom-left origin (Y grows upward); CSS percentages need a top-left
// origin, hence the (pageHeight - y) flip. null when the page's own Width/Height aren't
// known (pages persisted before that was added, or a non-PdfPig source) - callers must
// treat that as "can't highlight this one", not throw.
function toPercentBox(box: FieldBox, page: Record<string, any> | undefined) {
    if (!page?.width || !page?.height) return null;

    const topPct = ((page.height - box.top) / page.height) * 100;
    const bottomPct = ((page.height - box.bottom) / page.height) * 100;

    return {
        leftPct: (box.left / page.width) * 100,
        topPct,
        widthPct: ((box.right - box.left) / page.width) * 100,
        heightPct: bottomPct - topPct,
    };
}

export function InspectionReportModal({fileId, processRunId, onClose}: InspectionReportModalProps) {
    const [reportData, setReportData] = useState<MatchesResult | null>(null);
    const [wrInspectionReport, setWrInspectionReport] = useState<unknown>(null);
    const [wrInspectionReportError, setWrInspectionReportError] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [highlightBoxes, setHighlightBoxes] = useState<FieldBox[]>([]);
    const [lastClickInfo, setLastClickInfo] = useState<string | null>(null);

    const [position, setPosition] = useState({top: 40, left: 40});
    const [size, setSize] = useState({width: 'calc(100% - 80px)', height: 'calc(100% - 80px)'});

    const handleMaximize = () => {
        setPosition({top: 0, left: 0});
        setSize({width: '100%', height: '100%'});
    };

    const handleMinimize = () => {
        setPosition({top: 40, left: 40});
        setSize({width: 'calc(100% - 80px)', height: 'calc(100% - 80px)'});
    };

    const pdfScrollRef = useRef<HTMLDivElement>(null);
    const jsonContainerRef = useRef<HTMLDivElement>(null);
    const primaryHighlightRef = useRef<HTMLDivElement | null>(null);

    const selectedIndexRef = useRef<number>(-1);
    const selectedRowRef = useRef<HTMLElement | null>(null);

    useEffect(() => {
        setLoading(true);
        setWrInspectionReportError(null);
        setHighlightBoxes([]);
        setLastClickInfo(null);
        selectedIndexRef.current = -1;
        selectedRowRef.current = null;

        Promise.allSettled([
            waleApiClient.matchesResult(fileId),
            fetch(`${waleApiBaseUrl}/BFF/FileData/WrInspectionReportString?fileId=${fileId}&processRunId=${processRunId}`)
                .then(async response => {
                    const text = await response.text();
                    if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${text}`);
                    return text ? JSON.parse(text) : null;
                }),
        ]).then(([matchesResult, wrInspectionReportResult]) => {
            if (matchesResult.status === 'fulfilled') setReportData(matchesResult.value);

            if (wrInspectionReportResult.status === 'fulfilled') {
                setWrInspectionReport(wrInspectionReportResult.value);
            } else {
                console.error('Error fetching WrInspectionReport JSON:', wrInspectionReportResult.reason);
                setWrInspectionReportError(String(wrInspectionReportResult.reason));
            }
        }).finally(() => setLoading(false));
    }, [fileId, processRunId]);

    const fieldBoxes = useMemo(
        () => buildFieldBoxes((reportData as unknown as Record<string, any>)?.matches),
        [reportData]
    );

    const getPropertyRows = (): HTMLElement[] => {
        if (!jsonContainerRef.current) return [];

        return Array.from(jsonContainerRef.current.querySelectorAll<HTMLElement>('.json-view--pair'))
            .filter(row => row.querySelector(':scope > .json-view--property, :scope > .json-view--index'));
    };

    const getRowPropertyName = (row: Element): string => {
        const span = row.querySelector(':scope > .json-view--property, :scope > .json-view--index');
        return span?.textContent?.replace(/"/g, '') ?? '';
    };

    const applySelection = (propertyName: string) => {
        if (!propertyName) {
            setLastClickInfo(null);
            return;
        }

        const labelName = propertyName.charAt(0).toUpperCase() + propertyName.slice(1);
        const boxes = fieldBoxes.get(labelName) ?? [];
        setHighlightBoxes(boxes);

        if (boxes.length === 0) {
            setLastClickInfo(`"${propertyName}" isn't a matched field (container/derived value, or nothing extracted for it).`);
            return;
        }

        const anyPercent = boxes.some(b => toPercentBox(b, (reportData as unknown as Record<string, any>)?.pages
            ?.find((p: any) => p.number === b.pageNumber)) !== null);

        setLastClickInfo(anyPercent
            ? null
            : `"${propertyName}" matched, but this file's page size wasn't recorded (reprocess it to enable highlighting).`);

        // Scrolling itself happens in the effect below, once the highlight boxes below have
        // actually rendered - scrollIntoView on the highlight div (not the page image) handles
        // both "wrong page" and "right page but scrolled past the highlight" in one call,
        // rather than only bringing the page's top edge into view.
    };

    const selectRow = (row: HTMLElement, index: number) => {
        if (selectedRowRef.current) {
            selectedRowRef.current.style.outline = '';
            selectedRowRef.current.style.backgroundColor = '';
        }

        row.style.outline = '2px solid #1976d2';
        row.style.backgroundColor = 'rgba(25, 118, 210, 0.08)';
        row.scrollIntoView({block: 'nearest'});

        selectedRowRef.current = row;
        selectedIndexRef.current = index;

        applySelection(getRowPropertyName(row));
    };

    const handleJsonClick = (e: React.MouseEvent) => {
        const pairEle = (e.target as HTMLElement).closest('.json-view--pair') as HTMLElement | null;
        if (!pairEle) return;

        const index = getPropertyRows().indexOf(pairEle);
        if (index !== -1) selectRow(pairEle, index);
    };

    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;

            const active = document.activeElement;
            if (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement) return;

            const rows = getPropertyRows();
            if (rows.length === 0) return;

            e.preventDefault();

            const nextIndex = selectedIndexRef.current === -1
                ? 0
                : e.key === 'ArrowDown'
                    ? Math.min(selectedIndexRef.current + 1, rows.length - 1)
                    : Math.max(selectedIndexRef.current - 1, 0);

            selectRow(rows[nextIndex], nextIndex);
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    }, [fieldBoxes, reportData]);

    // The first highlight box (in matched-line order) that actually has page dimensions to
    // place it with - marked so it can be scrolled into view once rendered, regardless of
    // which page it's on or how far down that page it sits.
    const percentHighlightsByPage = useMemo(() => {
        const byPage = new Map<number, {percent: NonNullable<ReturnType<typeof toPercentBox>>, isPrimary: boolean}[]>();
        let primaryAssigned = false;

        for (const box of highlightBoxes) {
            const page = (reportData as unknown as Record<string, any>)?.pages
                ?.find((p: any) => p.number === box.pageNumber);
            const percent = toPercentBox(box, page);
            if (!percent) continue;

            const list = byPage.get(box.pageNumber) ?? [];
            list.push({percent, isPrimary: !primaryAssigned});
            primaryAssigned = true;
            byPage.set(box.pageNumber, list);
        }

        return byPage;
    }, [highlightBoxes, reportData]);

    // Runs after the highlight divs above have committed to the DOM (primaryHighlightRef is
    // only ever attached during render, so it's guaranteed current by the time this effect
    // fires) - centers the PDF pane's scroll on whichever page/position the highlight is at.
    useEffect(() => {
        primaryHighlightRef.current?.scrollIntoView({behavior: 'smooth', block: 'center'});
    }, [percentHighlightsByPage]);

    return (
        <DraggableModal
            id={0}
            position={position}
            size={size}
            onClose={onClose}
            onMaximize={handleMaximize}
            onMinimize={handleMinimize}
            onPositionChange={setPosition}
        >
            {loading && <div style={{padding: '20px'}}>Loading...</div>}

            {!loading && reportData && (
                <div style={{display: 'flex', height: '100%'}}>
                    <div style={{flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', borderRight: '1px solid #ddd'}}>
                        {lastClickInfo && (
                            <div style={{padding: '6px 20px', backgroundColor: '#fff8e1', borderBottom: '1px solid #ddd', fontSize: '0.85em', flexShrink: 0}}>
                                {lastClickInfo}
                            </div>
                        )}
                        <div
                            ref={jsonContainerRef}
                            style={{flex: 1, overflowY: 'auto', padding: '20px'}}
                            onClick={handleJsonClick}
                        >
                            {wrInspectionReportError
                                ? <div style={{color: 'red'}}>Error: {wrInspectionReportError}</div>
                                : <JsonView src={wrInspectionReport} collapsed={false} theme="default"/>}
                        </div>
                    </div>

                    <div style={{flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden'}}>
                        <div style={{padding: '10px 20px', borderBottom: '1px solid #ddd', flexShrink: 0}}>
                            <a href={getPdfUrl(reportData.filename)} target="_blank" rel="noopener noreferrer">
                                {reportData.filename}
                            </a>
                        </div>

                        <div ref={pdfScrollRef} style={{flex: 1, overflowY: 'auto', padding: '10px 20px'}}>
                            {Array.from({length: reportData.numberOfPages ?? 0}, (_, i) => i + 1).map(pageNum => {
                                const pageHighlights = percentHighlightsByPage.get(pageNum) ?? [];

                                return (
                                    <div key={pageNum} style={{position: 'relative', marginBottom: '10px'}}>
                                        <img
                                            id={`page${pageNum}`}
                                            src={getImageUrl(fileId, `${pageNum}`, 'PdfPig')}
                                            alt={`Page ${pageNum}`}
                                            style={{width: '100%', display: 'block'}}
                                            onError={(e) => {
                                                e.currentTarget.style.display = 'none';
                                            }}
                                        />
                                        {pageHighlights.map(({percent, isPrimary}, i) => (
                                            <div
                                                key={i}
                                                ref={isPrimary ? primaryHighlightRef : undefined}
                                                style={{
                                                    position: 'absolute',
                                                    left: `${percent.leftPct}%`,
                                                    top: `${percent.topPct}%`,
                                                    width: `${percent.widthPct}%`,
                                                    height: `${percent.heightPct}%`,
                                                    minHeight: '4px',
                                                    minWidth: '4px',
                                                    backgroundColor: 'rgba(255, 235, 59, 0.5)',
                                                    border: '1px solid rgba(255, 193, 7, 0.9)',
                                                    pointerEvents: 'none'
                                                }}
                                            />
                                        ))}
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                </div>
            )}
        </DraggableModal>
    );
}

export default InspectionReportModal;
