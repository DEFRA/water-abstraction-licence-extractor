import {LicenceFileMapEntry, LicenceSectionItemSummary, LinkedLicence, OutputListDataItem} from "../api/generated/apiClient.ts";

export const getFlaggedItem = (
    outputListDataItem: OutputListDataItem | undefined,
    licenceSectionName: string,
    licenceSectionItemId: string | undefined,
): LicenceSectionItemSummary | undefined => {
    if (!outputListDataItem || !licenceSectionItemId) return undefined;
    return (outputListDataItem.licenceSectionVerifications ?? [])
        .filter(s => s.licenceSectionName === licenceSectionName)
        .flatMap(s => s.licenceSectionItems ?? [])
        .find(i => i.licenceSectionItemId === licenceSectionItemId && !!i.isFlagged);
};

export const getVerificationTypeColor = (type: string): string =>
    getVerificationTypeBackgroundColor(type);

export const getVerificationWithNotesFirstPart = (value: string): string => {
    return value.split("::")[0];
};

// Value is "Type::ProcessRunId[::Notes]", or legacy "Type[::Notes]" without the process run
export const getVerificationWithNotesTooltip = (value: string): string => {
    const [type, second, ...rest] = value.split("::");
    const hasProcessRun = second !== undefined && /^\d+$/.test(second);
    const notes = hasProcessRun ? rest.join("::") : [second, ...rest].filter(x => x !== undefined).join("::");
    return `${type}${hasProcessRun ? ` (PR ${second})` : ''}${notes ? ` #${notes}` : ''}`;
};

export const getVerificationTypeBackgroundColor = (type: string): string => {
    switch (type) {
        case 'AutoConfirm':
            return '#00D100';
        case 'Confirmed':
            return '#008000';
        case 'CompleteBusinessReview':
            return '#004700';
        case 'Removed':
            return 'red';
        case 'AutoFail':
            return 'red';
        case 'Edited':
            return 'blue';
        case 'Added':
            return '#1890ff';
        case 'AutoWarn':
            return 'darkorange';
        case 'RequestBusinessReview':
            return 'darkorange';
        default:
            return 'inherit';
    }
};

export const getVerificationTypeInitials = (type: string): string => {
    switch (type) {
        case 'Confirmed':
            return 'C';
        case 'AutoConfirm':
            return 'AC';
        case 'Removed':
            return 'X';
        case 'Edited':
            return 'E';
        case 'Added':
            return '+';
        case 'AutoFail':
            return 'AF';
        case 'AutoWarn':
            return 'AW';
        case 'RequestBusinessReview':
            return 'BR';
        case 'CompleteBusinessReview':
            return 'BC';
        default:
            return '';
    }
};

export const hasOnlyOneOutgoingSection = (containedIn?: any[]): boolean => {
    if (!containedIn) return true;
    return containedIn.filter(s => s.direction === 'Outgoing').length <= 1;
};

export const hasAnyOutgoingSections = (containedIn?: any[]): boolean => {
    if (!containedIn) return false;
    return containedIn.filter(s => s.direction === 'Outgoing').length > 0;
};

export interface LinkedLicenceGroupEntry {
    licenceNumber: string;
    isScraped: boolean;
}

// Raw scraped numbers can differ from formatted ones by zero-padding and separators.
const normaliseLicenceNumber = (licenceNumber: string): string =>
    licenceNumber
        .toLowerCase()
        .split(/[/.\s-]+/)
        .filter(part => !!part)
        .map(part => part.replace(/^0+/, ''))
        .join('/');

// Groups licence numbers into follow-on chains, each a list of stages ordered oldest first. A stage
// normally holds one number, or several if a licence was split. History only decides the grouping and
// order: revisions missing from licenceNumbers link their neighbours but aren't returned themselves.
export const groupLinkedLicencesByFollowOn = (
    licenceNumbers: string[],
    linkedLicences: LinkedLicence[],
): LinkedLicenceGroupEntry[][][] => {
    const followOns = new Map<string, Set<string>>();
    const predecessors = new Map<string, Set<string>>();
    const addEdge = (edges: Map<string, Set<string>>, from: string, to: string) =>
        edges.set(from, (edges.get(from) ?? new Set<string>()).add(to));

    // Pooled across all linked licences, as each one may only hold part of its chain's history.
    linkedLicences
        .flatMap(ll => ll.containedIn ?? [])
        .flatMap(section => section.history ?? [])
        .forEach(item => {
            if (!item.licenceNumber) return;
            const from = normaliseLicenceNumber(item.licenceNumber);
            (item.followOnLicenceNumbers ?? []).forEach(followOn => {
                const to = normaliseLicenceNumber(followOn);
                addEdge(followOns, from, to);
                addEdge(predecessors, to, from);
            });
        });

    const getChain = (start: string): Set<string> => {
        const chain = new Set<string>();
        const pending = [start];
        while (pending.length > 0) {
            const current = pending.pop()!;
            if (chain.has(current)) continue;
            chain.add(current);
            pending.push(...(followOns.get(current) ?? []), ...(predecessors.get(current) ?? []));
        }
        return chain;
    };

    const getDepth = (key: string, visited: Set<string> = new Set()): number => {
        const nextVisited = new Set(visited).add(key);
        const depths = [...(predecessors.get(key) ?? [])]
            .filter(predecessor => !nextVisited.has(predecessor))
            .map(predecessor => getDepth(predecessor, nextVisited) + 1);
        return Math.max(0, ...depths);
    };

    const placed = new Set<string>();
    const groups: LinkedLicenceGroupEntry[][][] = [];

    licenceNumbers.filter(licenceNumber => !!licenceNumber).forEach(licenceNumber => {
        if (placed.has(normaliseLicenceNumber(licenceNumber))) return;

        const chain = getChain(normaliseLicenceNumber(licenceNumber));
        const members = licenceNumbers.filter(number => {
            const key = normaliseLicenceNumber(number);
            if (!number || !chain.has(key) || placed.has(key)) return false;
            placed.add(key);
            return true;
        });

        const memberKeys = new Set(members.map(normaliseLicenceNumber));
        const scrapedKeys = new Set(linkedLicences
            .filter(ll => ll.licenceNumber && memberKeys.has(normaliseLicenceNumber(ll.licenceNumber)))
            .map(ll => ll.rawScrapedLicenceNumber)
            .filter((raw): raw is string => !!raw)
            .map(normaliseLicenceNumber));

        const stages = new Map<number, LinkedLicenceGroupEntry[]>();
        members.forEach(number => {
            const key = normaliseLicenceNumber(number);
            const depth = getDepth(key);
            stages.set(depth, [...(stages.get(depth) ?? []), {licenceNumber: number, isScraped: scrapedKeys.has(key)}]);
        });

        groups.push([...stages.entries()].sort(([a], [b]) => a - b).map(([, stage]) => stage));
    });

    return groups;
};

export function getFileId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): string | false {
    if (!licenceNumber || !fileIdMap) {
        return false;
    }

    let list = fileIdMap[licenceNumber];
    
    if (!list) {
        return false;
    }
    
    return list[0].fileId ?? false;
}

export function getLicenceId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): number | undefined {
    if (!licenceNumber || !fileIdMap) {
        return undefined;
    }

    let list = fileIdMap[licenceNumber];

    if (!list) {
        return undefined;
    }

    return list[0].licenceId;
}

export function getMatchesResultId(fileIdMap: Record<string, LicenceFileMapEntry[]> | undefined, licenceNumber: string | undefined): number | undefined {
    if (!licenceNumber || !fileIdMap) {
        return undefined;
    }

    let list = fileIdMap[licenceNumber];

    if (!list) {
        return undefined;
    }

    return list[0].matchesResultId;
}
