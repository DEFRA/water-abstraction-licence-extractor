import { JSONPath } from 'jsonpath-plus';

interface AbstractionLimitsProps {
    reportData: any;
    onJumpToPage: (pageNumber: number) => void;
    onOpenLinkedLicence?: (filename: string) => void;
    level?: number;
}

export function AbstractionLimits({
                                      reportData,
                                      onJumpToPage,
                                      onOpenLinkedLicence,
                                      level = 0
                                  }: AbstractionLimitsProps) {
    // Prevent infinite recursion
    if (level > 3) return null;

    const isLinkedLicenceLevel = level > 0;

    // Get abstraction limits and means of abstraction
    const abstractionLimitsMatches = getMatches(
        reportData,
        '$.matches[?(@.labelGroupName==\'AbstractionLimits\')]'
    );
    const meansOfAbstractionMatches = getMatches(
        reportData,
        '$.matches[?(@.labelGroupName==\'MeansOfAbstraction\')]'
    );

    const hasAbstractionLimits =
        abstractionLimitsMatches != null && abstractionLimitsMatches.length > 0;
    const hasMeansOfAbstraction =
        meansOfAbstractionMatches != null && meansOfAbstractionMatches.length > 0;

    if (!hasAbstractionLimits && !hasMeansOfAbstraction) {
        return null;
    }

    return (
        <dl>
            {hasAbstractionLimits && (
                <AbstractionLimitsSection
                    abstractionLimitsMatches={abstractionLimitsMatches}
                    onJumpToPage={onJumpToPage}
                    onOpenLinkedLicence={onOpenLinkedLicence}
                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                    level={level}
                />
            )}

            {hasMeansOfAbstraction && (
                <MeansOfAbstractionSection
                    meansOfAbstractionMatches={meansOfAbstractionMatches}
                />
            )}
        </dl>
    );
}

// Helper functions
function getMatches(dataToUse: any, path: string): any[] {
    try {
        const results = JSONPath({ path, json: dataToUse });
        return results || [];
    } catch {
        return [];
    }
}

function getText(dataToUse: any, path: string): string | null {
    const matches = getMatches(dataToUse, path);
    if (matches.length === 0) return null;
    const matched = matches[0];
    if (!matched?.text || matched.text.length === 0) return null;
    return matched.text[0].text;
}

// Abstraction Limits Section
interface AbstractionLimitsSectionProps {
    abstractionLimitsMatches: any[];
    onJumpToPage: (pageNumber: number) => void;
    onOpenLinkedLicence?: (filename: string) => void;
    isLinkedLicenceLevel: boolean;
    level: number;
}

function AbstractionLimitsSection({
                                      abstractionLimitsMatches,
                                      onJumpToPage,
                                      onOpenLinkedLicence,
                                      isLinkedLicenceLevel,
                                      level
                                  }: AbstractionLimitsSectionProps) {
    const abstractionLimitsSection = abstractionLimitsMatches[0];
    const abstractionLimitsConditionBlocks = abstractionLimitsSection.subResults || [];

    return (
        <>
            <dt>
                <strong>Authorised quantities</strong>
            </dt>
            <dd id="abstractionLimits">
                <dl>
                    {abstractionLimitsConditionBlocks.map((conditionBlock: any, idx: number) => (
                        <ConditionBlock
                            key={idx}
                            conditionBlock={conditionBlock}
                            onJumpToPage={onJumpToPage}
                            onOpenLinkedLicence={onOpenLinkedLicence}
                            isLinkedLicenceLevel={isLinkedLicenceLevel}
                            level={level}
                        />
                    ))}
                </dl>
            </dd>
        </>
    );
}

// Condition Block
interface ConditionBlockProps {
    conditionBlock: any;
    onJumpToPage: (pageNumber: number) => void;
    onOpenLinkedLicence?: (filename: string) => void;
    isLinkedLicenceLevel: boolean;
    level: number;
}

function ConditionBlock({
                            conditionBlock,
                            onJumpToPage,
                            onOpenLinkedLicence,
                            isLinkedLicenceLevel,
                            level
                        }: ConditionBlockProps) {
    const subResults = conditionBlock.subResults || [];

    return (
        <>
            {subResults.map((conditionBlockSub: any, jdx: number) => {
                const conditionBlockPurpose = getText(
                    conditionBlockSub,
                    '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PointPurpose\')]'
                );

                const linkedLicenceNumbers = getMatches(
                    conditionBlockSub,
                    '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'LinkedLicenceNumber\')]'
                );

                const linkedLicences = getMatches(
                    conditionBlockSub,
                    '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'LinkedLicence\')]'
                );

                const linkedLicenceFilenames = getMatches(
                    conditionBlockSub,
                    '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'LinkedLicenceFilename\')]'
                );

                return (
                    <div key={jdx}>
                        <dt>
                            {conditionBlockPurpose ? (
                                isLinkedLicenceLevel ? (
                                    conditionBlockPurpose
                                ) : (
                                    <a
                                        href="#"
                                        onClick={(e) => {
                                            e.preventDefault();
                                            onJumpToPage(conditionBlockSub.pageNumber);
                                        }}
                                    >
                                        {conditionBlockPurpose}
                                    </a>
                                )
                            ) : linkedLicenceNumbers.length > 0 ? (
                                'In aggregation with other licences'
                            ) : (
                                'All specified period'
                            )}
                        </dt>
                        <dd>
                            <dl>
                                <QuantityLimit
                                    conditionBlockSub={conditionBlockSub}
                                    label="Per second"
                                    valuePath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerSecondValue')]"
                                    unitsPath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerSecondUnits')]"
                                    onJumpToPage={onJumpToPage}
                                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                                />
                                <QuantityLimit
                                    conditionBlockSub={conditionBlockSub}
                                    label="Per hour"
                                    valuePath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerHourValue')]"
                                    unitsPath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerHourUnits')]"
                                    onJumpToPage={onJumpToPage}
                                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                                />
                                <QuantityLimit
                                    conditionBlockSub={conditionBlockSub}
                                    label="Per day"
                                    valuePath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerDayValue')]"
                                    unitsPath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerDayUnits')]"
                                    onJumpToPage={onJumpToPage}
                                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                                />
                                <QuantityLimit
                                    conditionBlockSub={conditionBlockSub}
                                    label="Per month"
                                    valuePath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerMonthValue')]"
                                    unitsPath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerMonthUnits')]"
                                    onJumpToPage={onJumpToPage}
                                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                                />
                                <QuantityLimit
                                    conditionBlockSub={conditionBlockSub}
                                    label="Per year"
                                    valuePath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerYearValue')]"
                                    unitsPath="$.subResults[?(@.matchedLabel != null && @.matchedLabel.name=='PerYearUnits')]"
                                    onJumpToPage={onJumpToPage}
                                    isLinkedLicenceLevel={isLinkedLicenceLevel}
                                />

                                {/* Render linked licences */}
                                {linkedLicenceNumbers.map((linkedLicenceNumber: any, kdx: number) => {
                                    const licenceNumber = toText(linkedLicenceNumber);
                                    if (!licenceNumber) return null;

                                    const linkedLicenceFilename = toText(linkedLicenceFilenames[kdx]);
                                    const linkedLicence = linkedLicences[kdx];

                                    return (
                                        <LinkedLicenceBlock
                                            key={kdx}
                                            licenceNumber={licenceNumber}
                                            linkedLicenceFilename={linkedLicenceFilename}
                                            linkedLicence={linkedLicence}
                                            onOpenLinkedLicence={onOpenLinkedLicence}
                                            onJumpToPage={onJumpToPage}
                                            level={level}
                                        />
                                    );
                                })}
                            </dl>
                        </dd>
                    </div>
                );
            })}
        </>
    );
}

// Quantity Limit (Per second, Per hour, etc.)
interface QuantityLimitProps {
    conditionBlockSub: any;
    label: string;
    valuePath: string;
    unitsPath: string;
    onJumpToPage: (pageNumber: number) => void;
    isLinkedLicenceLevel: boolean;
}

function QuantityLimit({
                           conditionBlockSub,
                           label,
                           valuePath,
                           unitsPath,
                           onJumpToPage,
                           isLinkedLicenceLevel
                       }: QuantityLimitProps) {
    const value = getText(conditionBlockSub, valuePath);
    const units = getText(conditionBlockSub, unitsPath);

    if (!value) return null;

    const formattedValue = parseFloat(value).toLocaleString();

    return (
        <>
            <dt>
                <strong>{label}</strong>
            </dt>
            <dd>
                {isLinkedLicenceLevel ? (
                    `${formattedValue} ${units}`
                ) : (
                    <a
                        href="#"
                        onClick={(e) => {
                            e.preventDefault();
                            onJumpToPage(conditionBlockSub.pageNumber);
                        }}
                    >
                        {formattedValue} {units}
                    </a>
                )}
            </dd>
        </>
    );
}

// Linked Licence Block
interface LinkedLicenceBlockProps {
    licenceNumber: string;
    linkedLicenceFilename: string | null;
    linkedLicence: any;
    onOpenLinkedLicence?: (filename: string) => void;
    onJumpToPage: (pageNumber: number) => void;
    level: number;
}

function LinkedLicenceBlock({
                                licenceNumber,
                                linkedLicenceFilename,
                                linkedLicence,
                                onOpenLinkedLicence,
                                onJumpToPage,
                                level
                            }: LinkedLicenceBlockProps) {
    const filename = linkedLicenceFilename || '[NOT_FOUND]';
    const filenameLink = filename
        .replace('.pdf', '')
        .replace('.PDF', '')
        .replaceAll('.', '-');

    const linkedAssignedTo = linkedLicence
        ? getText(linkedLicence, '$.subResults[?(@.labelGroupName==\'Company\')]')
        : null;

    const linkedLicenceAbstractionLimitsArray = linkedLicence
        ? getMatches(linkedLicence, '$.subResults[?(@.labelGroupName==\'AbstractionLimits\')]')
        : [];

    const linkedLicenceAbstractionLimits = linkedLicenceAbstractionLimitsArray[0];
    const linkedLicenceAbstractionLimitsSubResults =
        linkedLicenceAbstractionLimits?.subResults || [];

    return (
        <>
            <dt>
                <strong>Linked licence</strong>
            </dt>
            <dd>
                <dl>
                    <dt>
                        <strong>Licence number</strong>
                    </dt>
                    <dd>
                        {filename !== '[NOT_FOUND]' && onOpenLinkedLicence ? (
                            <a
                                href="#"
                                onClick={(e) => {
                                    e.preventDefault();
                                    onOpenLinkedLicence(filenameLink);
                                }}
                            >
                                {licenceNumber}
                            </a>
                        ) : (
                            licenceNumber
                        )}
                    </dd>

                    {linkedAssignedTo && (
                        <>
                            <dt className="default-hidden">
                                <strong>Licence holder</strong>
                            </dt>
                            <dd className="default-hidden">{linkedAssignedTo}</dd>
                        </>
                    )}

                    {linkedLicenceAbstractionLimitsSubResults.length > 0 && (
                        <NestedAbstractionLimits
                            conditionBlocks={linkedLicenceAbstractionLimitsSubResults}
                            onJumpToPage={onJumpToPage}
                            onOpenLinkedLicence={onOpenLinkedLicence}
                            level={level + 1}
                        />
                    )}
                </dl>
            </dd>
        </>
    );
}

// Nested Abstraction Limits (for linked licences)
interface NestedAbstractionLimitsProps {
    conditionBlocks: any[];
    onJumpToPage: (pageNumber: number) => void;
    onOpenLinkedLicence?: (filename: string) => void;
    level: number;
}

function NestedAbstractionLimits({
                                     conditionBlocks,
                                     onJumpToPage,
                                     onOpenLinkedLicence,
                                     level
                                 }: NestedAbstractionLimitsProps) {
    return (
        <>
            <dt>
                <strong>Authorised quantities</strong>
            </dt>
            <dd>
                <dl>
                    {conditionBlocks.map((conditionBlock: any, idx: number) => (
                        <ConditionBlock
                            key={idx}
                            conditionBlock={conditionBlock}
                            onJumpToPage={onJumpToPage}
                            onOpenLinkedLicence={onOpenLinkedLicence}
                            isLinkedLicenceLevel={true}
                            level={level}
                        />
                    ))}
                </dl>
            </dd>
        </>
    );
}

// Means of Abstraction Section
interface MeansOfAbstractionSectionProps {
    meansOfAbstractionMatches: any[];
}

function MeansOfAbstractionSection({
                                       meansOfAbstractionMatches
                                   }: MeansOfAbstractionSectionProps) {
    const meansMatch = meansOfAbstractionMatches[0];

    const secondValue = getText(
        meansMatch,
        '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondValueMeans\')]'
    );

    const secondUnits = getText(
        meansMatch,
        '$.subResults[?(@.matchedLabel != null && @.matchedLabel.name==\'PerSecondUnitsMeans\')]'
    );

    if (!secondValue) return null;

    const meansText = meansMatch.text;
    const meansDescription = meansText?.length > 0 ? meansText[0].text : '--';
    const formattedValue = parseFloat(secondValue).toLocaleString();

    return (
        <>
            <dt>Means of abstraction ("{meansDescription}")</dt>
            <dd>
                <dl>
                    <dt>
                        <strong>Per second</strong>
                    </dt>
                    <dd>
                        {formattedValue} {secondUnits}
                    </dd>
                </dl>
            </dd>
        </>
    );
}

// Helper to convert matched object to text
function toText(matched: any): string | null {
    if (!matched?.text || matched.text.length === 0) return null;
    return matched.text[0].text;
}

export default AbstractionLimits;