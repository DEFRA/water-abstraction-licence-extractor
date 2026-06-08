import {ReportContent} from './ReportContent';
import {LicenceSetReportContent} from './LicenceSetReportContent';
import {DraggableModal} from "./DraggableModal";
import type {ReportModal} from "../utils/types.ts";

interface ReportModalContainerProps {
    modals: ReportModal[];
    onClose: (id: number) => void;
    onMaximize: (id: number) => void;
    onMinimize: (id: number) => void;
    onPositionChange: (id: number, position: { top: number; left: number }) => void;
    onRefresh?: () => void;
    //onOpenLinkedLicence: (fileId: string) => void;
}

export function ReportModalContainer({
        modals,
        onClose,
        onMaximize,
        onMinimize,
        onPositionChange,
        onRefresh/*,
        onOpenLinkedLicence*/,
    }: ReportModalContainerProps) {
    return (
        <>
            {modals.map(modal => (
                <DraggableModal
                    key={modal.id}
                    id={modal.id}
                    position={modal.position}
                    size={modal.size}
                    onClose={() => onClose(modal.id)}
                    onMaximize={() => onMaximize(modal.id)}
                    onMinimize={() => onMinimize(modal.id)}
                    onPositionChange={(pos) => onPositionChange(modal.id, pos)}
                >
                    {modal.type === 'report' ? (
                        <ReportContent 
                            fileId={modal.fileId}
                            /*onOpenLinkedLicence={onOpenLinkedLicence}*/
                            processRunId={modal.processRunId}
                            onRefresh={onRefresh}
                            outputListDataItem={modal.outputListDataItem}
                        />
                    ) : (
                        <LicenceSetReportContent
                            fileId={modal.fileId}
                            licenceSetId={modal.licenceSetId!}
                        />
                    )}
                </DraggableModal>
            ))}
        </>
    );
}
