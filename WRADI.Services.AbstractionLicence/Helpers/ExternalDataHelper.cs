using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Helpers;
using WRADI.Core.AbstractionLicence.Models;
using FormattingHelper = WALE.ProcessFile.Core.Helpers.FormattingHelper;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, List<NaldAbstractionData>> TransformNaldData(
        NaldDataCollection data,
        Dictionary<string, DmsFileData> licenceNumbersWithFilenames)
    {
        var returnList = new Dictionary<string, NaldAbstractionData>();
        var internalLicenceIdsNotInDataset = new HashSet<string>();

        foreach (var line in data.AbstractionLicences!)
        {
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo, line.FgacRegionCode)!;
            var key = $"{line.FgacRegionCode}|{line.Id}";

            if (!licenceNumbersWithFilenames.ContainsKey(stippedLicenceNumber))
            {
                internalLicenceIdsNotInDataset.Add(key);
                continue;
            }

            if (returnList.TryGetValue(key, out _))
            {
                throw new Exception("Repeat row");
            }

            var naldData = NaldHelper.NaldAbstractionLicenceDataLineToNaldData(line);
            returnList.Add(key, naldData!);
        }

        // Ensure versions are handled first as the other data depends on the licence version (issueNo, incrNo)
        AddNaldAbstractionLicenceVersionData(
            data.AbstractionLicenceVersions!,
            internalLicenceIdsNotInDataset,
            ref returnList);

        AddNaldAbstractionLicenceQuantitiesData(
            data.AbstractionLicenceQuantities!,
            internalLicenceIdsNotInDataset,
            ref returnList);
        
        var purposeToLicenceMapping = AddNaldAbstractionLicencePurposeData(
            data.AbstractionLicencePurposes!,
            internalLicenceIdsNotInDataset,
            ref returnList);
        
        AddNaldAbstractionLicencePointsData(
            data.AbstractionLicencePoints!,
            ref purposeToLicenceMapping);

        var changedKeyList = new Dictionary<string, List<NaldAbstractionData>>();

        foreach (var (_, naldData) in returnList)
        {
            var key = naldData.FgacRegionCode + "|" + naldData.LicenceIdCharsAndDigitsOnly;

            if (changedKeyList.TryGetValue(key, out var value))
            {
                value.Add(naldData);
                continue;
            }

            changedKeyList.Add(key, [naldData]);
        }

        return changedKeyList;
    }
    
    private static void AddNaldAbstractionLicenceVersionData(
        List<NaldLicenceVersionDataLine> naldCurrentVersionDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldAbstractionData> generalNaldData)
    {
        foreach (var versionDataLine in naldCurrentVersionDataLines
            .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(versionDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(versionDataLine.LookupKey);
            }

            NaldHelper.AddNaldAbstractionLicenceVersionData(versionDataLine, naldData);
        }
    }

    private static void AddNaldAbstractionLicenceQuantitiesData(
        List<NaldLicenceQuantitiesDataLine> naldLicenceQuantitiesDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldAbstractionData> generalNaldData)
    {
        foreach (var quantitiesDataLine in naldLicenceQuantitiesDataLines
            .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(quantitiesDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(quantitiesDataLine.LookupKey);
            }

            // Ignore non-current quantity data
            if (naldData.IncrNo != quantitiesDataLine.AabvIncrNo
                || naldData.IssueNo != quantitiesDataLine.AabvIssueNo)
            {
                continue;
            }
            
            NaldHelper.AddNaldAbstractionLicenceQuantitiesData(quantitiesDataLine, naldData);
        }
    }

    private static Dictionary<string, NaldAbstractionData> AddNaldAbstractionLicencePurposeData(
        List<NaldLicencePurposeDataLine> naldLicencePurposeDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldAbstractionData> generalNaldData)
    {
        var returnDict = new Dictionary<string, NaldAbstractionData>();

        foreach (var purposeDataLine in naldLicencePurposeDataLines
            .Where(x => !licenceNumbersNotInDataset.Contains(x.LicenceIdLookupKey)))
        {
            if (!generalNaldData.TryGetValue(purposeDataLine.LicenceIdLookupKey, out var naldData))
            {
                throw new KeyNotFoundException(purposeDataLine.LicenceIdLookupKey);
            }

            // Ignore non-current purpose data
            if (naldData.IncrNo != purposeDataLine.AabvIncrNo ||
                naldData.IssueNo != purposeDataLine.AabvIssueNo)
            {
                continue;
            }

            NaldHelper.AddNaldAbstractionLicencePurposeData(purposeDataLine, naldData);
        }

        return returnDict;
    }

    private static void AddNaldAbstractionLicencePointsData(
        List<NaldLicencePointDataLine> naldLicencePointDataLines,
        ref Dictionary<string, NaldAbstractionData> purposeToLicenceMapping)
    {
        foreach (var pointDataLine in naldLicencePointDataLines)
        {
            if (!purposeToLicenceMapping.TryGetValue(pointDataLine.PurposeIdLookupKey, out var naldData))
            {
                // Just skip it - might be a purpose ID linked to a non-current version
                continue;
            }

            NaldHelper.AddNaldAbstractionLicencePointsData(pointDataLine, naldData);
        }
    }
}