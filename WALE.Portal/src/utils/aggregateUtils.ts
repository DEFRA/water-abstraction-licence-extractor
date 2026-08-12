import type { Aggregate } from '../api/generated/apiClient.ts';

/**
 * TS mirror of the composite `Id` getter on WRADI.Core.AbstractionLicence/Models/Aggregate.cs.
 * Keep this in sync with the C# source — there's no compile-time link between the two.
 */
export function computeAggregateId(aggregate: Aggregate): string {
    const primaryAbbreviation: Record<string, string> = {
        NotSet: 'NS',
        InLicence: 'IL',
        LicenceToLicence: 'LL'
    };
    const subAbbreviation: Record<string, string> = {
        NotSet: 'NS',
        PurposeToPurpose: 'PU',
        PointToPoint: 'PO'
    };

    const stripSeparators = (value: string) => value.replace(/\//g, '').replace(/ /g, '');

    const primary = primaryAbbreviation[aggregate.primaryType ?? 'NotSet'] ?? '';
    const sub = subAbbreviation[aggregate.subType ?? 'NotSet'] ?? '';
    const licenceNumber = stripSeparators(aggregate.sourceLicenceNumber ?? '');
    const linkedSuffix = (aggregate.linkedLicences ?? [])
        .map(ll => `-${stripSeparators(ll)}`)
        .join('');

    return `${licenceNumber}-${aggregate.sourceLicenceVersionId}-${primary}${sub}${linkedSuffix}`;
}
