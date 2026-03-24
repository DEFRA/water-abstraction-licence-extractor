import type {Licence} from "../api/generated/apiClient.ts";
import {LicenceSection} from "./LicenceSection.tsx";
import {LinkedLicences} from "./LinkedLicences.tsx";

interface VerificationContentProps {
    licence: Licence;
}

export function VerificationContent({ licence }: VerificationContentProps) {
    return (
        <div id="properties" style={{ padding: '10px' }}>
            <LicenceSection 
                title="Linked Licences" 
                licenceFileId={licence.dmsFileId!} 
                processRunId={licence.processRunId!}
            >
                <LinkedLicences 
                    licence={licence} 
                    isEditing={false} // This will be overridden by LicenceSection's React.cloneElement
                />
            </LicenceSection>
        </div>
    );
}
