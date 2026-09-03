using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class NaldPurposesHelper
{
    public static (NaldPurposeData[] Purposes, string? MatchType) GetRelevantNaldPurposes(
        List<NaldPurposeData> naldPurposes,
        string? documentDescription,
        List<string> excludeNaldPurposeIds)
    {
        var filterPurposes = naldPurposes
            .Where(p => !excludeNaldPurposeIds.Contains(p.Id!))
            .ToList();

        if (filterPurposes.Count == 0)
        {
            return ([], null);
        }
        
        var groupedPurposes = filterPurposes
            .GroupBy(pu => $"{pu.CombinedCode}_{pu.QuantityIdentifier}")
            .ToList();
     
        var descriptionSuggestsTransfer =
            documentDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
            || documentDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true;

        documentDescription = FormattingHelper.TrimFormatting(
            documentDescription,
            true,
            true);

        if (string.IsNullOrWhiteSpace(documentDescription))
        {
            throw new Exception("Document description is empty");
        }
        
        foreach (var loopNaldPurposes in groupedPurposes)
        {
            if (excludeNaldPurposeIds.Contains(loopNaldPurposes.First().Id!))
            {
                continue;
            }

            var firstNaldPurpose = loopNaldPurposes.First();
            
            if (CheckExplicitPurposeMapping(
                firstNaldPurpose.PrimaryCategoryDescription,
                firstNaldPurpose.SecondaryCategoryDescription,
                firstNaldPurpose.UseDescription,
                documentDescription))
            {
                return (loopNaldPurposes.ToArray(), "ExplicitMapping");
            }
            
            if (documentDescription.Equals(firstNaldPurpose.PrimaryCategoryDescription, StringComparison.OrdinalIgnoreCase)
                || documentDescription.Equals(firstNaldPurpose.SecondaryCategoryDescription, StringComparison.OrdinalIgnoreCase)
                || documentDescription.Equals(firstNaldPurpose.UseDescription, StringComparison.OrdinalIgnoreCase))
            {
                return (loopNaldPurposes.ToArray(), "DescriptionMatchesDescription");
            }
            
            if (descriptionSuggestsTransfer)
            {
                var naldSuggestsTransfer =
                    firstNaldPurpose.UseDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.PrimaryCategoryDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.SecondaryCategoryDescription?.Contains("transfer", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.UseDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.PrimaryCategoryDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true
                    || firstNaldPurpose.SecondaryCategoryDescription?.Contains("subsequent", StringComparison.OrdinalIgnoreCase) == true;

                if (naldSuggestsTransfer)
                {
                    return (loopNaldPurposes.ToArray(), "DescriptionSuggestsTransfer");
                }
            }

            if (firstNaldPurpose.UseDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true
                || firstNaldPurpose.PrimaryCategoryDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true
                || firstNaldPurpose.SecondaryCategoryDescription?.Contains(documentDescription, StringComparison.OrdinalIgnoreCase) == true)
            {
                return (loopNaldPurposes.ToArray(), "DescriptionContainsDescription");
            }
        }
        
        // There is only one, so must be that
        if (groupedPurposes.Count == 1)
        {
            return (groupedPurposes[0].ToArray(), "OnlyOne");
        }

        return ([], null);
    }

    public static List<NaldPurposeData> ToNaldPurposeData(List<NaldDataPurpose>? purposes)
    {
        return purposes?
            .Select(purpose => new NaldPurposeData
            {
                Id = purpose.Id.ToString(),
                PrimaryCategoryDescription = purpose.CategoryUse.PrimaryCategoryDescription,
                SecondaryCategoryDescription = purpose.CategoryUse.SecondaryCategoryDescription,
                UseDescription = purpose.CategoryUse.UseDescription,
                PrimaryCategoryCode = purpose.CategoryUse.PrimaryCategoryCode.ToString(),
                SecondaryCategoryCode = purpose.CategoryUse.SecondaryCategoryCode.ToString(),
                UseCode = purpose.CategoryUse.UseCode.ToString(),
                CombinedCode = purpose.CategoryUse.Code,
                QuantityIdentifier = $"{purpose.Quantity.AnnualQty}_{purpose.Quantity.DailyQty}" +
                    $"_{purpose.Quantity.HourlyQty}_{purpose.Quantity.InstQty}"
            })
            .ToList() ?? [];
    }
    
    private static bool CheckExplicitPurposeMapping(
        string? naldPrimaryCategoryDescription,
        string? naldSecondaryCategoryDescription,
        string? naldUseDescription,
        string? documentDescription)
    {
        if (string.IsNullOrEmpty(documentDescription))
        {
            return false;
        }
        
        // Key is document purpose description, Value is Nald purpose name
        var documentToNaldPurposeMapping = new Dictionary<string, string[]>
        {
            { "agriculture (other than spray irrigation)", ["general farming & domestic"] },
            { "reservoir storage for subsequent stream compensation", ["transfer between sources (pre water act 2003)"] },
            { "private water supply", [
                "general use relating to secondary category (very low loss)",
                "general use relating to secondary category (low loss)",
                "general use relating to secondary category (medium loss)",
                "general use relating to secondary category (high loss)"
            ]},
            { "domestic & sanitation", ["drinking, cooking, sanitary, washing, (small garden) - commercial/industrial/public services"]},
            { "ground source heating and cooling pump", ["heat pump"]},
            { "domestic", ["drinking, cooking, sanitary, washing, (small garden) - commercial/industrial/public Services"]},
        };

        var documentDescriptionLower = documentDescription.ToLower();
        var documentPurposeIsMapped = documentToNaldPurposeMapping.ContainsKey(documentDescriptionLower);

        if (!documentPurposeIsMapped)
        {
            return false;
        }

        var mappedNaldValues = documentToNaldPurposeMapping[documentDescriptionLower];

        return mappedNaldValues.Any(v => v.Equals(naldPrimaryCategoryDescription, StringComparison.OrdinalIgnoreCase))
            || mappedNaldValues.Any(v => v.Equals(naldSecondaryCategoryDescription, StringComparison.OrdinalIgnoreCase))
            || mappedNaldValues.Any(v => v.Equals(naldUseDescription, StringComparison.OrdinalIgnoreCase));
    }
}