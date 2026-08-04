import { useState, useCallback } from 'react';
import type {ReportModal} from "./types.ts";
import {OutputListDataItem} from "../api/generated/apiClient.ts";

export function useReportModals() {
    const [modals, setModals] = useState<ReportModal[]>([]);
    const [modalCounter, setModalCounter] = useState(0);

    const openReport = useCallback((fileId: string, processRunId: number, item?: OutputListDataItem, data?: OutputListDataItem[], onOpenReport?: (fileId: string) => void) => {
        const newModal: ReportModal = {
            id: modalCounter,
            type: 'report',
            fileId,
            processRunId,
            position: { top: 40, left: 350 },
            size: { width: 'calc(100% - 370px)', height: 'calc(100% - 60px)' },
            outputListDataItem: item,
            data,
            onOpenReport
        };

        setModals(prev => [...prev, newModal]);
        setModalCounter(prev => prev + 1);
    }, [modalCounter]);

    const openLicenceSetReport = useCallback((fileId: string, licenceSetId: string, processRunId: number) => {
        const newModal: ReportModal = {
            id: modalCounter,
            type: 'licenceSet',
            fileId,
            licenceSetId,
            processRunId,
            position: { top: 40, left: 350 },
            size: { width: 'calc(100% - 370px)', height: 'calc(100% - 60px)' }
        };

        setModals(prev => [...prev, newModal]);
        setModalCounter(prev => prev + 1);
    }, [modalCounter]);

    const closeModal = useCallback((id: number) => {
        setModals(prev => prev.filter(modal => modal.id !== id));
    }, []);

    const updateModalPosition = useCallback((id: number, position: { top: number; left: number }) => {
        setModals(prev => prev.map(modal =>
            modal.id === id ? { ...modal, position } : modal
        ));
    }, []);

    const maximizeModal = useCallback((id: number) => {
        setModals(prev => prev.map(modal =>
            modal.id === id
                ? {
                    ...modal,
                    position: { top: 0, left: 0 },
                    size: { width: '100%', height: '100%' }
                }
                : modal
        ));
    }, []);

    const minimizeModal = useCallback((id: number) => {
        setModals(prev => prev.map(modal =>
            modal.id === id
                ? {
                    ...modal,
                    position: { top: 40, left: 350 },
                    size: { width: 'calc(100% - 370px)', height: 'calc(100% - 60px)' }
                }
                : modal
        ));
    }, []);

    const updateModalOutputItem = useCallback((fileId: string, item: OutputListDataItem) => {
        setModals(prev => prev.map(modal =>
            (modal.type === 'report' && modal.fileId === fileId)
                ? { ...modal, outputListDataItem: item }
                : modal
        ));
    }, []);

    return {
        modals,
        openReport,
        openLicenceSetReport,
        closeModal,
        updateModalPosition,
        maximizeModal,
        minimizeModal,
        updateModalOutputItem
    };
}