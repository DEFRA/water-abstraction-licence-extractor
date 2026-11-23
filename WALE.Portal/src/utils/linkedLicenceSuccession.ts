import type {LinkedLicence} from "../api/generated/apiClient.ts";

export interface LinkedLicenceSuccession {
    current: LinkedLicence;
    superseded: LinkedLicence[];
}

// containedIn.history is generated as any[], so the shape is narrowed here rather than at the call.
interface HistoryEntry {
    licenceNumber?: string;
    followOnLicenceNumbers?: string[];
}

function followOnNumbers(linkedLicence: LinkedLicence): string[] {
    const numbers = new Set<string>();

    for (const containedIn of linkedLicence.containedIn ?? []) {
        for (const entry of (containedIn.history ?? []) as HistoryEntry[]) {
            // Only this licence's own history counts. A merged containedIn can carry another
            // licence's chain, which would otherwise fold unrelated licences together.
            if (entry.licenceNumber !== linkedLicence.licenceNumber) {
                continue;
            }

            for (const number of entry.followOnLicenceNumbers ?? []) {
                if (number !== linkedLicence.licenceNumber) {
                    numbers.add(number);
                }
            }
        }
    }

    return [...numbers];
}

// A chain that never reaches an unsuperseded number is circular; the licence then stays on its own
// rather than disappearing from the list.
function newestInChain(
    licenceNumber: string,
    followOns: Map<string, string[]>,
    superseded: Set<string>): string | undefined {

    const seen = new Set<string>([licenceNumber]);
    let current = licenceNumber;

    for (;;) {
        const next = (followOns.get(current) ?? []).find(number => !seen.has(number));

        if (!next) {
            return undefined;
        }

        if (!superseded.has(next)) {
            return next;
        }

        seen.add(next);
        current = next;
    }
}

export function groupBySuccession(linkedLicences: LinkedLicence[]): LinkedLicenceSuccession[] {
    const present = new Set(
        linkedLicences.map(linkedLicence => linkedLicence.licenceNumber).filter(Boolean) as string[]);

    const followOns = new Map<string, string[]>();
    const superseded = new Set<string>();

    for (const linkedLicence of linkedLicences) {
        const licenceNumber = linkedLicence.licenceNumber;

        if (!licenceNumber) {
            continue;
        }

        const presentFollowOns = followOnNumbers(linkedLicence).filter(number => present.has(number));
        followOns.set(licenceNumber, presentFollowOns);

        if (presentFollowOns.length) {
            superseded.add(licenceNumber);
        }
    }

    const groups = new Map<string, LinkedLicenceSuccession>();
    const orphans: LinkedLicenceSuccession[] = [];

    for (const linkedLicence of linkedLicences) {
        const licenceNumber = linkedLicence.licenceNumber;

        if (!licenceNumber || !superseded.has(licenceNumber)) {
            continue;
        }

        const newest = newestInChain(licenceNumber, followOns, superseded);

        if (!newest) {
            orphans.push({current: linkedLicence, superseded: []});
            continue;
        }

        const group = groups.get(newest) ?? {current: linkedLicences.find(
            candidate => candidate.licenceNumber === newest)!, superseded: []};

        group.superseded.push(linkedLicence);
        groups.set(newest, group);
    }

    const result: LinkedLicenceSuccession[] = [];

    for (const linkedLicence of linkedLicences) {
        const licenceNumber = linkedLicence.licenceNumber;

        if (licenceNumber && superseded.has(licenceNumber)) {
            continue;
        }

        result.push(
            (licenceNumber && groups.get(licenceNumber)) ?? {current: linkedLicence, superseded: []});
    }

    return [...result, ...orphans];
}
