import {OutputListDataItem, OutputListDataItemLicenceSet} from "../api/generated/apiClient.ts";
import {useEffect, useState, useMemo, type ReactNode} from "react";
import {getThumbnailUrl} from "../utils/images.ts";
import {getLicenceSetTypeClass} from "../utils/licenceSetTypeUtils.ts";

interface LicenceSetsTableBodyProps {
    data: OutputListDataItem[];
    onOpenReport: (filename: string) => void;
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void;
    onTotalsCalculated?: (totals: LicenceSetsTotals) => void;
}

export interface LicenceSetsTotals {
    licenceSetsCount: number;
    licenceSetTypesCount: number;
    licenceSetsLicenceNumberCount: number;
    licenceSetsFilenameCount: number;
}

interface ExtendedLicenceSet extends OutputListDataItemLicenceSet {
    descendants: ExtendedLicenceSet[];
    ancestors: ExtendedLicenceSet[];
}

export function LicenceSetsTableBody({data, onOpenReport, onOpenLicenceSetReport, onTotalsCalculated}: LicenceSetsTableBodyProps) {
    const [hierarchicalData, setHierarchicalData] = useState<ExtendedLicenceSet[]>([]);
    
    useEffect(() => {
        const allLicenceSets = data.flatMap(item => item.licenceSets || []);
        const uniqueLicenceSetIds = new Set<string>();
        const licenceSets: ExtendedLicenceSet[] = allLicenceSets
            .filter(ls => {
                if (ls.licenceSetId && !uniqueLicenceSetIds.has(ls.licenceSetId)) {
                    uniqueLicenceSetIds.add(ls.licenceSetId);
                    return true;
                }
                return false;
            })
            .map(ls => Object.assign(ls, {
                descendants: [],
                ancestors: []
            }) as ExtendedLicenceSet);

        setHierarchy(licenceSets);
        setHierarchicalData(licenceSets);
    }, [data]);

    // Move all calculations into useMemo to prevent unnecessary work and potential loops
    const { rows, totals } = useMemo(() => {
        const generatedRows: React.ReactNode[] = [];
        let rowIndex = 0;
        const stats = {
            licenceSetsCount: 0,
            licenceSetTypesCount: 0,
            licenceSetsLicenceNumberCount: 0,
            licenceSetsFilenameCount: 0
        };

        const processRowRecursive = (licenceSet: ExtendedLicenceSet, indentLevel: number) => {
            const currentRowsCountBefore = generatedRows.length;
            
            // Assuming renderListRow returns a component or pushes to the array
            // Note: If renderListRow pushes to the array, ensure it adds a unique 'key'
            renderListRow(licenceSet, rowIndex++, generatedRows, data, indentLevel, onOpenReport, onOpenLicenceSetReport);
            
            const rowsAdded = generatedRows.length - currentRowsCountBefore;
            
            stats.licenceSetsCount++;
            stats.licenceSetTypesCount += licenceSet.licenceSetTypes?.length ?? 0;
            stats.licenceSetsLicenceNumberCount += rowsAdded;

            const licencesInSet = getLicencesInSet(data, licenceSet.licenceSetId!);
            stats.licenceSetsFilenameCount += licencesInSet.filter(l => l.filename !== '--').length;

            // Handle children recursively
            const children = getChildrenOf(hierarchicalData, licenceSet);
            children.forEach(child => processRowRecursive(child, indentLevel + 1));
        };

        const topLevel = getChildrenOf(hierarchicalData, null);
        topLevel.forEach(ls => processRowRecursive(ls, 0));

        return { rows: generatedRows, totals: stats };
    }, [hierarchicalData, data, onOpenReport, onOpenLicenceSetReport]);

    // Trigger callback only when totals actually change
    useEffect(() => {
        onTotalsCalculated?.(totals);
    }, [totals, onTotalsCalculated]);

    return (
        <tbody>
            {rows}
        </tbody>
    );
}

function setHierarchy(licenceSets: ExtendedLicenceSet[]) {
    licenceSets.sort((a, b) => (b.shortLicenceSetId?.length ?? 0) - (a.shortLicenceSetId?.length ?? 0));

    for (let i = 0; i < licenceSets.length; i++) {
        let licenceSetA = licenceSets[i];
        let shortLicenceIdsA = licenceSetA.shortLicenceSetId?.split('-') ?? [];

        for (let j = 0; j < licenceSets.length; j++) {
            if (i === j) continue;

            let licenceSetB = licenceSets[j];
            let shortLicenceIdsB = licenceSetB.shortLicenceSetId?.split('-') ?? [];

            if (shortLicenceIdsA.length <= shortLicenceIdsB.length) continue;

            let bContainsLicenceNotInA = false;
            for (let k = 0; k < shortLicenceIdsB.length; k++) {
                let shortLicenceIdB = shortLicenceIdsB[k];
                if (shortLicenceIdsA.indexOf(shortLicenceIdB) === -1) {
                    bContainsLicenceNotInA = true;
                    break;
                }
            }

            if (!bContainsLicenceNotInA) {
                if (licenceSetA.descendants.find(cl => cl.shortLicenceSetId === licenceSetB.shortLicenceSetId) === undefined) {
                    licenceSetA.descendants.push(licenceSetB);
                }
                if (licenceSetB.ancestors.find(cl => cl.shortLicenceSetId === licenceSetA.shortLicenceSetId) === undefined) {
                    licenceSetB.ancestors.push(licenceSetA);
                }
            }
        }
    }
}

function getChildrenOf(licenceSets: ExtendedLicenceSet[], licenceSet: ExtendedLicenceSet | null) {
    return licenceSets.filter(x => {
        if (!licenceSet) {
            return x.ancestors.length === 0;
        }
        if (x.ancestors.length === 0) {
            return false;
        }

        const sortedAncestors = [...x.ancestors].sort((a, b) => (b.shortLicenceSetId?.length ?? 0) - (a.shortLicenceSetId?.length ?? 0));
        let shortestId = sortedAncestors[sortedAncestors.length - 1];

        return shortestId.shortLicenceSetId === licenceSet.shortLicenceSetId;
    });
}

function renderListRow(
    licenceSet: ExtendedLicenceSet,
    i: number,
    rows: ReactNode[],
    dataSorted: OutputListDataItem[],
    indentLevel: number,
    onOpenReport: (filename: string) => void,
    onOpenLicenceSetReport: (filename: string, licenceSetId: string) => void
) {
    const oddRow = i % 2 === 0;
    const backgroundColor = oddRow ? "#F6F6F6" : "#FAFAFA";
    const licenceSetId = licenceSet.licenceSetId!;

    const licencesInSet = getLicencesInSet(dataSorted, licenceSetId);
    const firstLicenceInSet = licencesInSet[0];

    const licenceInSetType = getLicenceSetTypeClass(firstLicenceInSet.licenceSets?.find(x => x.licenceSetId === licenceSetId)?.licenceSetType);

    rows.push(
        <tr key={`${licenceSetId}-0`} style={{backgroundColor}} className={`indent-level-${indentLevel}`}>
            <td rowSpan={licencesInSet.length}>
                <div className='indentDiv'></div>
                {licencesInSet.map((item, idx) => (
                    item.imagePath === undefined ? (
                        <div key={idx} style={{display: 'inline-block', width: '57px', textAlign: 'center', fontSize: '80px', lineHeight: '60px', verticalAlign: 'top', color: '#EEE'}}>--</div>
                    ) : (
                        <img key={idx} src={getThumbnailUrl(item.fileId)} style={{height: '80px'}} alt='No image found' onError={(e) => e.currentTarget.style.display = 'none'} />
                    )
                ))}
            </td>
            <td rowSpan={licencesInSet.length}>
                <span className='lsId' title={licenceSetId}>
                    <a href='#' onClick={(e) => {
                        e.preventDefault();
                        onOpenLicenceSetReport(licencesInSet[0].filename!, licenceSetId);
                    }}>
                        {licenceSet.shortLicenceSetId}
                    </a>
                </span>
            </td>
            <td rowSpan={licencesInSet.length}>
                <ul>
                    {licenceSet.licenceSetTypes?.map((type, idx) => (
                        <li key={idx} className={`ls-type ${getLicenceSetTypeClass(type)}`}>{getLicenceSetTypeClass(type)}</li>
                    ))}
                </ul>
            </td>
            <td className={licenceInSetType}>{firstLicenceInSet.licenceNumber}</td>
            <td className='filename-cell'>
                {firstLicenceInSet.filename !== '--' ? (
                    <a href='#' onClick={(e) => {
                        e.preventDefault();
                        onOpenReport(firstLicenceInSet.filename!);
                    }} className='filenameSet'>
                        {firstLicenceInSet.filename}
                    </a>
                ) : "--"}
            </td>
        </tr>
    );

    for (let j = 1; j < licencesInSet.length; j++) {
        const licenceInSet = licencesInSet[j];
        const currentLicenceInSetType = getLicenceSetTypeClass(licenceInSet.licenceSets?.find(x => x.licenceSetId === licenceSetId)?.licenceSetType);

        rows.push(
            <tr key={`${licenceSetId}-${j}`} style={{backgroundColor}} className={`indent-level-${indentLevel}`}>
                <td className={currentLicenceInSetType}>{licenceInSet.licenceNumber}</td>
                <td className='filename-cell'>
                    {licenceInSet.filename !== '--' ? (
                        <a href='#' onClick={(e) => {
                            e.preventDefault();
                            onOpenReport(licenceInSet.filename!);
                        }} className='filenameSet'>
                            {licenceInSet.filename}
                        </a>
                    ) : "--"}
                </td>
            </tr>
        );
    }
}

function getLicencesInSet(dataSorted: OutputListDataItem[], licenceSetId: string) {
    let returnList: OutputListDataItem[] = [];
    let licenceNumbers: string[] = [];

    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let ary = item.licenceSets || [];

        for (let j = 0; j < ary.length; j++) {
            let value = ary[j].licenceSetId;

            if (value === licenceSetId && licenceNumbers.indexOf(item.licenceNumber!) === -1) {
                returnList.push(item);
                licenceNumbers.push(item.licenceNumber!);
            }
        }
    }

    let parts = licenceSetId.split('-');

    for (let i = 0; i < parts.length; i += 2) {
        let shortLicenceNumber = parts[i];
        let fullLicenceNumber = getFullLicenceNumber(dataSorted, shortLicenceNumber);

        if (licenceNumbers.indexOf(fullLicenceNumber) === -1) {
            let licence = dataSorted.find(item => item.licenceNumber === fullLicenceNumber) || {
                filename: '--',
                licenceNumber: fullLicenceNumber,
            } as OutputListDataItem;

            licenceNumbers.push(fullLicenceNumber);
            returnList.push(licence);
        }
    }

    returnList.sort((a, b) => {
        if (a.licenceNumber! < b.licenceNumber!) return -1;
        if (a.licenceNumber! > b.licenceNumber!) return 1;
        return 0;
    });

    return returnList;
}

function getFullLicenceNumber(dataSorted: OutputListDataItem[], shortLicenceNumber: string) {
    for (let i = 0; i < dataSorted.length; i++) {
        let item = dataSorted[i];
        let licenceNumberStripped = item.licenceNumber!
            .replaceAll("/", "")
            .replaceAll(" ", "")
            .replaceAll(".", "");

        if (licenceNumberStripped === shortLicenceNumber) {
            return item.licenceNumber!;
        }

        let ary = item.linkedLicences || [];

        for (let j = 0; j < ary.length; j++) {
            let value = ary[j].licenceNumber!;

            let linkedLicenceNumberStripped = value
                .replaceAll("/", "")
                .replaceAll(" ", "")
                .replaceAll(".", "");

            if (linkedLicenceNumberStripped === shortLicenceNumber) {
                return value;
            }
        }
    }

    return shortLicenceNumber;
}

export default LicenceSetsTableBody;
