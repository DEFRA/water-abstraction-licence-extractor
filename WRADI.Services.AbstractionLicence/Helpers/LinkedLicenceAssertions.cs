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
                var licenceSetIdParts = licenceSet.LicenceSetId?
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
                        throw new Exception($"Licence set mentions licence {licenceSetIdPart} that isn't in linked licences");
                    }
                }
            }
        }
    }

    public static void FixNoLicenceSetWithoutLinkedLicence(IEnumerable<Licence> licences)
    {
        var allLicences = licences.ToList();
        
        var okayLicences = allLicences
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
                var licenceSetIdParts = licenceSet.LicenceSetId?
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
                        var licenceToAdd = allLicences.FirstOrDefault(ll => ll.LicenceNumber!.Value!
                            .Replace(".", string.Empty)
                            .Replace("-", string.Empty)
                            .Replace("/", string.Empty)                        
                            .Replace(" ", string.Empty) == licenceSetIdPart);

                        if (licenceToAdd == null)
                        {
                            throw new Exception($"Cant find licence to add {licenceToAdd}");
                        }

                        var newLinkedLicences = licence.LinkedLicences.ToList();
                        newLinkedLicences.Add(new LinkedLicence
                        {
                            LicenceNumber = licenceToAdd.LicenceNumber!.Value,
                            RegionId = licenceToAdd.RegionId,
                            ContainedIn = [
                                new()
                                {
                                    Direction = InformationDirection.InSameGroup,
                                    Source = InformationSource.Unknown // TODO - maybe could infer this
                                }],
                            NaldStatus = licenceToAdd.NaldStatus,
                            Filename = licenceToAdd.Filename,
                            DmsPath =  licenceToAdd.DmsPath,
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
