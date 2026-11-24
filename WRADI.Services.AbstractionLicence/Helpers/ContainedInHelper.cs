using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class ContainedInHelper
{
    public static void AddFollowOnContainedIns(LinkedLicence linkedLicence, LinkedLicence existing)
    {
        var currentContainedIn = (existing.ContainedIn ?? []).ToList();

        foreach (var currentContained in linkedLicence.ContainedIn?.ToList() ?? [])
        {
            var toAdd =
                currentContained.History?.Any(item =>
                    item.FollowOnLicenceNumbers.Contains(existing.LicenceNumber!)) == true
                    ? AsFollowOn(currentContained)
                    // A copy: sharing the instance lets a later History assignment on one
                    // licence stamp its chain onto the other.
                    : currentContained.Clone();

            if (currentContainedIn.Any(containedIn =>
                AreContainedInFundamentallyEqual(containedIn, toAdd)))
            {
                continue;
            }

            currentContainedIn.Add(toAdd);
        }

        existing.ContainedIn = currentContainedIn.ToArray();
    }

    public static bool AreContainedInFundamentallyEqual(
        ContainedInInformation containedIn1,
        ContainedInInformation containedIn2)
    {
        return containedIn1.SectionName == containedIn2.SectionName
               && containedIn1.LinkReason == containedIn2.LinkReason
               && containedIn1.LineNumber == containedIn2.LineNumber
               && containedIn1.PageNumber == containedIn2.PageNumber
               && containedIn1.Source == containedIn2.Source;
    }

    private static ContainedInInformation AsFollowOn(ContainedInInformation containedIn)
    {
        return new ContainedInInformation
        {
            Source = GetFollowOnSource(containedIn.Source),
            Direction = containedIn.Direction,
            SectionName = containedIn.SectionName,
            LinkReason = containedIn.LinkReason,
            LineNumber = containedIn.LineNumber,
            PageNumber = containedIn.PageNumber
        };
    }

    private static InformationSource GetFollowOnSource(InformationSource currentContainedSource)
    {
        return currentContainedSource switch
        {
            InformationSource.Unknown => InformationSource.UnknownFollowOn,
            InformationSource.Nald => InformationSource.NaldFollowOn,
            InformationSource.Document => InformationSource.DocumentFollowOn,
            InformationSource.OtherDocument => InformationSource.OtherDocumentFollowOn,
            InformationSource.MixedSourcesOrMixedReasons => InformationSource.MixedFollowOn,
            _ => throw new ArgumentOutOfRangeException(nameof(currentContainedSource), currentContainedSource, null)
        };
    }
}
