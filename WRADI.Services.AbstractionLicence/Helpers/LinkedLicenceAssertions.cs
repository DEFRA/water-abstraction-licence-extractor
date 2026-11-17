using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class LinkedLicenceAssertions
{
    public static void AssertOrFixNoLicenceIsLinkedToItself(IEnumerable<Licence> licences, bool fixWhenDetected)
    {
        var okayLicences = licences
            .Where(l => l.Status == ScrapeStatus.Ok)
            .ToList();

        foreach (var licence in okayLicences)
        {
            if (licence.LicenceNumber == null)
            {
                continue;
            }

            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }

            foreach (var aggregate in licence.AbstractionLimits.Aggregates!)
            {
                if (aggregate.LinkedLicences == null
                    || aggregate.SourceLicenceNumber != licence.LicenceNumber.Value)
                {
                    continue;
                }

                var newLinkedLicences = new List<string>();
                
                foreach (var linkedLicence in aggregate.LinkedLicences)
                {
                    if (linkedLicence == licence.LicenceNumber.Value)
                    {
                        if (fixWhenDetected)
                        {
                            continue;
                        }
                        
                        throw new Exception("Licence has aggregate licence linking to itself");
                    }
                    
                    newLinkedLicences.Add(linkedLicence);
                }
                
                aggregate.LinkedLicences = newLinkedLicences.ToArray();
            }
        }
    }

    public static void AssertNoLicenceSetWithoutLinkedLicence(IEnumerable<Licence> licences)
    {
        var okayLicences = licences
            .Where(l => l.Status == ScrapeStatus.Ok)
            .ToList();

        foreach (var licence in okayLicences)
        {
            if (licence.LicenceNumber == null)
            {
                continue;
            }

            var licenceNumber = licence.LicenceNumber!.Value!
                .Replace(".", string.Empty)
                .Replace("-", string.Empty)
                .Replace("/", string.Empty)
                .Replace(" ", string.Empty);

            foreach (var licenceSet in licence.LicenceSets)
            {
                var licenceSetIdParts = (licenceSet.LicenceSetId?[3..]!)
                    .Split('-')
                    .Where(p => p != licenceNumber)
                    .ToList();

                foreach (var licenceSetIdPart in licenceSetIdParts!)
                {
                    var hasLinkedLicence = licence.LinkedLicences.Any(ll => ll.LicenceNumber!
                        .Replace(".", string.Empty)
                        .Replace("-", string.Empty)
                        .Replace("/", string.Empty)
                        .Replace(" ", string.Empty) == licenceSetIdPart);

                    if (!hasLinkedLicence)
                    {
                        throw new Exception(
                            $"Licence set mentions licence {licenceSetIdPart} that isn't in linked licences");
                    }
                }
            }
        }
    }

    public static void FixNoLicenceSetWithoutLicenceHavingLinkedLicence(IEnumerable<Licence> licences)
    {
        var allLicences = licences.ToList();

        var okayLicences = allLicences
            .Where(l => l.Status == ScrapeStatus.Ok)
            .ToList();

        var uniqueLicences = allLicences
            .GroupBy(l => l.LicenceNumber?.Value)
            .Select(g => g.First())
            .ToList();

        foreach (var licence in okayLicences)
        {
            if (licence.LicenceNumber == null)
            {
                continue;
            }

            var licenceNumber = licence.LicenceNumber!.Value!
                .Replace(".", string.Empty)
                .Replace("-", string.Empty)
                .Replace("/", string.Empty)
                .Replace(" ", string.Empty);

            foreach (var licenceSet in licence.LicenceSets)
            {
                var licenceSetIdParts = (licenceSet.LicenceSetId?[3..]!)
                    .Split('-')
                    .Where(p => p != licenceNumber)
                    .ToList();

                foreach (var licenceSetIdPart in licenceSetIdParts)
                {
                    var hasLinkedLicence = licence.LinkedLicences.Any(ll => ll.LicenceNumber!
                        .Replace(".", string.Empty)
                        .Replace("-", string.Empty)
                        .Replace("/", string.Empty)
                        .Replace(" ", string.Empty) == licenceSetIdPart);

                    if (!hasLinkedLicence)
                    {
                        var licenceToAdd = uniqueLicences.FirstOrDefault(ll => ll.LicenceNumber!.Value!
                            .Replace(".", string.Empty)
                            .Replace("-", string.Empty)
                            .Replace("/", string.Empty)
                            .Replace(" ", string.Empty) == licenceSetIdPart);

                        if (licenceToAdd == null)
                        {
                            throw new Exception($"Cant find licence to add {licenceToAdd}");
                        }

                        var newLinkedLicences = licence.LinkedLicences.ToList();

                        var licencesReferencingBoth = uniqueLicences
                            .Where(al =>
                            {
                                var contains1 =
                                    al.LinkedLicences.Any(ll => ll.LicenceNumber == licence.LicenceNumber.Value);
                                var contains2 = al.LinkedLicences.Any(ll =>
                                    ll.LicenceNumber == licenceToAdd.LicenceNumber?.Value);

                                return contains1 && contains2;
                            })
                            .ToList();

                        var containedIn = licencesReferencingBoth
                            .SelectMany(l =>
                            {
                                var containedInList = new List<ContainedInInformation>();

                                var innerLinkedLicences = l.LinkedLicences
                                    .Where(ll => ll.LicenceNumber == licence.LicenceNumber.Value)
                                    .ToList();

                                foreach (var ll in innerLinkedLicences)
                                {
                                    if (ll.ContainedIn == null)
                                    {
                                        return containedInList;
                                    }

                                    foreach (var ci in ll.ContainedIn!)
                                    {
                                        if (ci.Direction != InformationDirection.Outgoing)
                                        {
                                            continue;
                                        }

                                        var cloned = ci.Clone();
                                        cloned.Direction = InformationDirection.ReferencedAlongside;
                                        cloned.SourceLicence = l.LicenceNumber?.Value;

                                        containedInList.Add(cloned);
                                    }
                                }

                                return containedInList;
                            })
                            .ToArray();

                        newLinkedLicences.Add(new LinkedLicence
                        {
                            LicenceNumber = licenceToAdd.LicenceNumber!.Value,
                            RegionId = licenceToAdd.RegionId,
                            ContainedIn = containedIn,
                            NaldStatus = licenceToAdd.NaldStatus,
                            Filename = licenceToAdd.Filename,
                            DmsPath = licenceToAdd.DmsPath,
                            DmsFileId = licenceToAdd.DmsFileId,
                            DmsPermitNumber = licenceToAdd.DmsPermitNumber,
                            LicenceType = licenceToAdd.LicenceType,
                            LicenceVersion = licenceToAdd.LicenceVersion
                        });

                        licence.LinkedLicences = newLinkedLicences.ToArray();
                    }
                }
            }
        }
    }

    public static void AssertNoDuplicateLinkedLicences(string[] linkedLicences)
    {
        var grouped = linkedLicences.GroupBy(l => l);
        
        foreach (var group in grouped)
        {
            if (group.Count() >= 2)
            {
                throw new Exception("Duplicate linked licence detected");
            }
        }
    }
}
