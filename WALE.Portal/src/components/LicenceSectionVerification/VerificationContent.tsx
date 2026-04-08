import type {Licence} from "../../api/generated/apiClient.ts";
import {LicenceSection} from "./LicenceSection";
import {LinkedLicences} from "./LinkedLicences";

interface VerificationContentProps {
    licence: Licence;
    processRunId: number;
    onJumpToPage: (pageNumber: number) => void;
}

export function VerificationContent({ licence, processRunId, onJumpToPage }: VerificationContentProps) {
    return (
        <div id="properties" style={{ padding: '10px' }}>
            <LicenceSection 
                title="Linked Licences" 
                licenceFileId={licence.dmsFileId!} 
                processRunId={processRunId}
            >
                <LinkedLicences 
                    licence={licence} 
                    isEditing={false} // This will be overridden by LicenceSection's React.cloneElement
                    onJumpToPage={onJumpToPage}
                />
            </LicenceSection>
        </div>
    );
}
