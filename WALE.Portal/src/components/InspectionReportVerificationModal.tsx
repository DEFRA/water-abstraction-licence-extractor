import {useEffect, useMemo, useRef, useState} from 'react';
import JsonView from 'react18-json-view';
import 'react18-json-view/src/style.css';
import {DraggableModal} from './DraggableModal';
import {waleApiClient} from '../api/apiClient';
import {getImageUrl, getPdfUrl} from '../utils/images';
import type {
    IDocumentLineWordCoordinates,
    ILabelGroupResult,
    IMatchesResult2,
    IPdfPage,
} from '../api/generated/apiClient';
import type {
    InspectionRecord,
    VerificationType,
} from '../api/wrInspectionReportVerificationModel';

interface Props {
    record: InspectionRecord;
    /// The extracted report, fetched by the page because the record's sections are built from it.
    form: Record<string, unknown> | null;
    formLoading: boolean;
    formError: string | null;
    onVerifySection: (sectionName: string, verificationType: VerificationType, override?: unknown) => void;
    onFlagRecord: () => void;
    onNextRecord: () => void;
    onClose: () => void;
}

interface FieldRect {
    left: number;
    right: number;
    top: number;
    bottom: number;
}

interface FieldBox extends FieldRect {
    pageNumber: number;
}

const TYPE_COLOURS: Record<VerificationType, string> = {
    Unverified: '#888',
    Confirmed: '#2e7d32',
    Edited: '#1565c0',
    ConfirmNone: '#6a1b9a',
    BusinessReview: '#c62828',
};

/// Every edge of a word's rectangle, with the optionality of the generated model resolved. A word
/// missing any edge is dropped rather than defaulted: a zero would outline the page corner, which
/// reads as a located value. The negative check is the extractor's own "not known" marker.
function toRect(coordinates: IDocumentLineWordCoordinates | undefined): FieldRect | null {
    const {left, right, top, bottom} = coordinates ?? {};

    if (left === undefined || right === undefined || top === undefined || bottom === undefined) {
        return null;
    }

    return left >= 0 && right >= 0 ? {left, right, top, bottom} : null;
}

/// Mirrors InspectionReportModal: a label's matched words give the region to outline.
function buildFieldBoxes(matches: ILabelGroupResult[] | undefined): Map<string, FieldBox[]> {
    const map = new Map<string, FieldBox[]>();

    const visit = (groups: ILabelGroupResult[] | undefined) => {
        if (!Array.isArray(groups)) return;

        for (const group of groups) {
            const name = group.matchedLabelName;
            const lines = group.text;

            if (name && lines?.length && !map.has(name)) {
                const boxes: FieldBox[] = [];

                for (const line of lines) {
                    const rects = (line.columns ?? [])
                        .flatMap(column => column.words ?? [])
                        .map(word => toRect(word.coordinates))
                        .filter((rect): rect is FieldRect => rect !== null);

                    if (rects.length === 0) continue;

                    boxes.push({
                        // 1-based in the model, so an unrecorded page matches no rendered page.
                        pageNumber: line.pageNumber ?? 0,
                        left: Math.min(...rects.map(rect => rect.left)),
                        right: Math.max(...rects.map(rect => rect.right)),
                        top: Math.max(...rects.map(rect => rect.top)),
                        bottom: Math.min(...rects.map(rect => rect.bottom)),
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

function toPercentBox(box: FieldBox, page: IPdfPage | undefined) {
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

export function InspectionReportVerificationModal(
    {record, form, formLoading, formError, onVerifySection, onFlagRecord, onNextRecord, onClose}: Props
) {
    const [activeSectionName, setActiveSectionName] = useState(record.sections[0]?.name ?? '');
    const [editing, setEditing] = useState(false);
    const [draft, setDraft] = useState('');
    const [draftError, setDraftError] = useState<string | null>(null);

    const [reportData, setReportData] = useState<IMatchesResult2 | null>(null);
    const [matchesLoading, setMatchesLoading] = useState(true);

    const [highlightBoxes, setHighlightBoxes] = useState<FieldBox[]>([]);
    const [trackingNote, setTrackingNote] = useState<string | null>(null);
    const jsonContainerRef = useRef<HTMLDivElement>(null);
    const selectedRowRef = useRef<HTMLElement | null>(null);
    const selectedIndexRef = useRef<number>(-1);

    const [position, setPosition] = useState({top: 40, left: 40});
    const [size, setSize] = useState({width: 'calc(100% - 80px)', height: 'calc(100% - 80px)'});

    // Only the matched words are read here; they drive the page overlay, nothing else uses them.
    useEffect(() => {
        setMatchesLoading(true);
        setReportData(null);

        waleApiClient.matchesResultString(record.fileId)
            .then(text => setReportData(text ? JSON.parse(text) as IMatchesResult2 : null))
            .catch(() => setReportData(null))
            .finally(() => setMatchesLoading(false));
    }, [record.fileId]);

    const activeSection = record.sections.find(section => section.name === activeSectionName)
        ?? record.sections[0];

    const fieldBoxes = useMemo(() => buildFieldBoxes(reportData?.matches), [reportData]);

    // The rail's section names are the form's own property names; the matched label is the same
    // name capitalised, as InspectionReportModal derives it from a clicked JSON row.
    const pages = reportData?.pages;

    /// Shared by a rail click and a JSON row click: a property name maps to the label of the same
    /// name capitalised, which is how the report modal resolves a clicked row.
    const selectProperty = (propertyName: string) => {
        if (!propertyName) {
            setHighlightBoxes([]);
            setTrackingNote(null);
            return;
        }

        const labelName = propertyName.charAt(0).toUpperCase() + propertyName.slice(1);
        const boxes = fieldBoxes.get(labelName) ?? [];
        setHighlightBoxes(boxes);

        if (boxes.length === 0) {
            setTrackingNote(`"${propertyName}" has no region to show - it is a container or derived `
                + "value, nothing was extracted for it, or it was resolved from a table cell, which "
                + "carries no word coordinates.");
            return;
        }

        setTrackingNote(boxes.some(box => toPercentBox(box, pages?.find(p => p.number === box.pageNumber)) !== null)
            ? null
            : `"${propertyName}" matched, but this file's page size was not recorded - reprocess it `
                + "to enable highlighting.");
    };

    useEffect(() => {
        if (!activeSection) {
            return;
        }

        selectProperty(activeSection.name);
        highlightJsonRowFor(activeSection.name);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [activeSectionName, fieldBoxes, form]);

    const getPropertyRows = () => {
        if (!jsonContainerRef.current) return [];

        return Array.from(jsonContainerRef.current.querySelectorAll<HTMLElement>('.json-view--pair'))
            .filter(row => row.querySelector(':scope > .json-view--property, :scope > .json-view--index'));
    };

    /// The outline only. Separate from selectRow so the rail can mark a row without re-deriving
    /// the property from it, which would call back into the rail and loop.
    const highlightRow = (row: HTMLElement, index: number) => {
        if (selectedRowRef.current) {
            selectedRowRef.current.style.outline = '';
            selectedRowRef.current.style.backgroundColor = '';
        }

        row.style.outline = '2px solid #1976d2';
        row.style.backgroundColor = 'rgba(25, 118, 210, 0.08)';
        row.scrollIntoView({block: 'nearest'});

        selectedRowRef.current = row;
        selectedIndexRef.current = index;
    };

    const selectRow = (row: HTMLElement, index: number) => {
        highlightRow(row, index);

        const span = row.querySelector(':scope > .json-view--property, :scope > .json-view--index');
        selectProperty(span?.textContent?.replace(/"/g, '') ?? '');
    };

    /// The other direction: a section picked in the rail marks its own node in the JSON. A node is
    /// only found when react18-json-view has rendered it, so a property nested inside a collapsed
    /// object - the three that live under "metadata" - has no row to mark, and the existing
    /// highlight is left where it is rather than being cleared to nothing.
    const highlightJsonRowFor = (propertyName: string) => {
        const rows = getPropertyRows();

        const index = rows.findIndex(row => row
            .querySelector(':scope > .json-view--property')
            ?.textContent
            ?.replace(/"/g, '') === propertyName);

        if (index !== -1) {
            highlightRow(rows[index], index);
        }
    };

    const handleJsonClick = (event: React.MouseEvent) => {
        const pair = (event.target as HTMLElement).closest('.json-view--pair') as HTMLElement | null;
        if (!pair) return;

        const index = getPropertyRows().indexOf(pair);
        if (index !== -1) selectRow(pair, index);
    };

    useEffect(() => {
        const handleKeyDown = (event: KeyboardEvent) => {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') return;

            const active = document.activeElement;
            if (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement) return;

            const rows = getPropertyRows();
            if (rows.length === 0) return;

            event.preventDefault();

            const next = selectedIndexRef.current === -1
                ? 0
                : event.key === 'ArrowDown'
                    ? Math.min(selectedIndexRef.current + 1, rows.length - 1)
                    : Math.max(selectedIndexRef.current - 1, 0);

            selectRow(rows[next], next);
        };

        window.addEventListener('keydown', handleKeyDown);
        return () => window.removeEventListener('keydown', handleKeyDown);
    });

    const outstanding = record.sections.filter(section => section.verificationType === 'Unverified');
    const verifiedCount = record.sections.length - outstanding.length;

    // Confirming moves to the next section still needing a decision.
    const advance = () => {
        const next = record.sections.find(section =>
            section.verificationType === 'Unverified' && section.name !== activeSectionName);

        if (next) setActiveSectionName(next.name);
    };

    const verify = (verificationType: VerificationType) => {
        onVerifySection(activeSection.name, verificationType);
        setEditing(false);
        advance();
    };

    const saveEdit = () => {
        try {
            const parsed = JSON.parse(draft) as unknown;
            setDraftError(null);
            onVerifySection(activeSection.name, 'Edited', parsed);
            setEditing(false);
            advance();
        } catch (error) {
            setDraftError(error instanceof Error ? error.message : 'Invalid JSON');
        }
    };

    const startEdit = () => {
        setDraft(JSON.stringify(activeSection.override ?? activeSection.scraped ?? {}, null, 2));
        setDraftError(null);
        setEditing(true);
    };

    const pageCount = reportData?.numberOfPages ?? 0;

    return (
        <DraggableModal
            id={0}
            position={position}
            size={size}
            onClose={onClose}
            onMaximize={() => { setPosition({top: 0, left: 0}); setSize({width: '100%', height: '100%'}); }}
            onMinimize={() => setSize({width: '520px', height: '360px'})}
            onPositionChange={setPosition}
        >
            <div style={{display: 'flex', flexDirection: 'column', height: '100%', fontSize: '12px'}}>
                <div style={{
                    padding: '8px 12px', borderBottom: '1px solid #ddd', display: 'flex',
                    justifyContent: 'space-between', alignItems: 'center', flexShrink: 0,
                }}>
                    <div>
                        <strong>{record.originalFileName}</strong>
                        <div style={{color: '#555', fontSize: '11px'}}>
                            {record.permitNumber} | run {record.processRunId} | {record.template}
                            {' | '}{record.isScan ? 'scan' : 'native'}
                            {' | '}{verifiedCount} of {record.sections.length} sections verified
                        </div>
                    </div>
                    <div style={{display: 'flex', gap: '6px'}}>
                        <button onClick={onFlagRecord}>Flag for business review</button>
                        <button onClick={onNextRecord} disabled={outstanding.length > 0}>
                            Verify &amp; next
                        </button>
                    </div>
                </div>

                <div style={{flex: 1, display: 'flex', minHeight: 0}}>
                    <div style={{width: '190px', borderRight: '1px solid #ddd', overflowY: 'auto', flexShrink: 0}}>
                        {record.sections.map(section => (
                            <div
                                key={section.name}
                                onClick={() => { setActiveSectionName(section.name); setEditing(false); }}
                                style={{
                                    padding: '6px 8px', cursor: 'pointer',
                                    borderLeft: section.name === activeSectionName
                                        ? '3px solid #1565c0' : '3px solid transparent',
                                    background: section.name === activeSectionName ? '#e3f2fd' : undefined,
                                }}>
                                <div>{section.label}</div>
                                <div style={{fontSize: '10px', color: TYPE_COLOURS[section.verificationType]}}>
                                    {section.verificationType}
                                    {section.reExtracted != null
                                        && <span style={{color: '#e65100'}}> - re-run differs</span>}
                                </div>
                            </div>
                        ))}
                    </div>

                    <div style={{
                        flex: '1 1 40%', minWidth: 0, display: 'flex', flexDirection: 'column',
                        borderRight: '1px solid #ddd',
                    }}>
                        {trackingNote && (
                            <div style={{
                                padding: '6px 8px', background: '#fff8e1', borderBottom: '1px solid #ddd',
                                fontSize: '11px', flexShrink: 0,
                            }}>
                                {trackingNote}
                            </div>
                        )}
                        <div
                            ref={jsonContainerRef}
                            onClick={handleJsonClick}
                            style={{flex: 1, overflow: 'auto', padding: '8px'}}>
                            {editing
                                ? (
                                    <>
                                        <textarea
                                            value={draft}
                                            onChange={event => setDraft(event.target.value)}
                                            style={{width: '100%', height: '240px', fontFamily: 'monospace', fontSize: '11px'}}
                                        />
                                        {draftError && <div style={{color: '#c62828'}}>{draftError}</div>}
                                        <button onClick={saveEdit}>Save correction</button>
                                        <button onClick={() => setEditing(false)}>Cancel</button>
                                    </>
                                )
                                : formLoading
                                    ? <em>Loading the extracted report...</em>
                                    : formError
                                        ? <span style={{color: '#c62828'}}>{formError}</span>
                                        : <JsonView src={form ?? activeSection.scraped ?? {}} collapsed={2}/>}
                        </div>

                        <div style={{borderTop: '1px solid #ddd', padding: '6px 8px', display: 'flex', gap: '6px'}}>
                            <button onClick={() => verify('Confirmed')}>Confirm</button>
                            <button onClick={startEdit}>Edit</button>
                            <button onClick={() => verify('ConfirmNone')}>None</button>
                        </div>
                    </div>

                    <div style={{flex: '1 1 60%', minWidth: 0, display: 'flex', flexDirection: 'column'}}>
                        <div style={{padding: '8px 12px', borderBottom: '1px solid #ddd', flexShrink: 0}}>
                            {reportData?.filename
                                ? <a href={getPdfUrl(reportData.filename)} target="_blank" rel="noopener noreferrer">
                                    {reportData.filename}
                                  </a>
                                : <span style={{color: '#888'}}>{matchesLoading ? 'Loading...' : 'No document'}</span>}
                        </div>

                        <div style={{flex: 1, overflowY: 'auto', padding: '10px 12px', background: '#f3f3f3'}}>
                            {Array.from({length: pageCount}, (_, index) => index + 1).map(pageNumber => {
                                const page = pages?.find(p => p.number === pageNumber);

                                return (
                                    <div key={pageNumber} style={{position: 'relative', marginBottom: '10px'}}>
                                        <img
                                            src={getImageUrl(record.fileId, `${pageNumber}`, 'PdfPig')}
                                            alt={`Page ${pageNumber}`}
                                            style={{width: '100%', display: 'block'}}
                                            onError={event => { event.currentTarget.style.display = 'none'; }}
                                        />
                                        {highlightBoxes
                                            .filter(box => box.pageNumber === pageNumber)
                                            .map((box, index) => {
                                                const percent = toPercentBox(box, page);
                                                if (percent === null) return null;

                                                return (
                                                    <div
                                                        key={index}
                                                        style={{
                                                            position: 'absolute',
                                                            left: `${percent.leftPct}%`,
                                                            top: `${percent.topPct}%`,
                                                            width: `${percent.widthPct}%`,
                                                            height: `${percent.heightPct}%`,
                                                            border: '2px solid #1976d2',
                                                            background: 'rgba(25, 118, 210, 0.12)',
                                                        }}
                                                    />
                                                );
                                            })}
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                </div>
            </div>
        </DraggableModal>
    );
}

export default InspectionReportVerificationModal;
