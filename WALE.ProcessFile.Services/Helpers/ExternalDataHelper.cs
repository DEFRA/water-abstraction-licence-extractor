using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class ExternalDataHelper
{
    public static Dictionary<string, List<NaldData>> TransformNaldData(
        NaldDataCollection data,
        Dictionary<string, DmsFileData> licenceNumbersWithFilenames)
    {
        var returnList = new Dictionary<string, NaldData>();
        var internalLicenceIdsNotInDataset = new HashSet<string>();
        
        foreach (var line in data.AbstractionLicences!)
        {
            var stippedLicenceNumber = FormattingHelper.StripForComparison(line.LicenceNo)!;
            
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

            var naldData = new NaldData
            {
                Id = line.Id,
                ExpiryDate = line.ExpiryDate,
                RevocationDate = line.RevDate,
                OrigEffDate = line.OrigEffectiveDate,
                OrigSigDate = line.OrigSignatureDate,
                LicenceNumber = line.LicenceNo!,
                LicenceIdCharsAndDigitsOnly = stippedLicenceNumber,
                FgacRegionCode = line.FgacRegionCode,
                ArepEiucCode = line.ArepEiucCode
            };

            returnList.Add(key, naldData);
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

        var changedKeyList = new Dictionary<string, List<NaldData>>();

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

    private static string? RemoveNullWord(string? value)
    {
        return value == "null" ? null : value;
    }

    private static void AddNaldAbstractionLicenceVersionData(
        List<NaldLicenceVersionDataLine> naldCurrentVersionDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var versionDataLine in naldCurrentVersionDataLines
            .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(versionDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(versionDataLine.LookupKey);
            }

            naldData.AabvType = versionDataLine.AabvType;
            naldData.EffEndDate = versionDataLine.EffEndDate;
            naldData.EffStDate = versionDataLine.EffStDate;
            naldData.LicSigDate = versionDataLine.LicSigDate;
            naldData.IncrNo = versionDataLine.IncrNo;
            naldData.AppNo = versionDataLine.AppNo;
            naldData.IssueNo = versionDataLine.IssueNo;
            naldData.Status = versionDataLine.Status;
            naldData.WaAltyCode = versionDataLine.WaAltyCode;
            naldData.AsrcCode = versionDataLine.AsrcCode;
        }
    }

    private static void AddNaldAbstractionLicenceQuantitiesData(
        List<NaldLicenceQuantitiesDataLine> naldLicenceQuantitiesDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        foreach (var quantitiesDataLine in naldLicenceQuantitiesDataLines
                     .Where(x => !licenceNumbersNotInDataset.Contains(x.LookupKey)))
        {
            if (!generalNaldData.TryGetValue(quantitiesDataLine.LookupKey, out var naldData))
            {
                throw new KeyNotFoundException(quantitiesDataLine.LookupKey);
            }

            // Ignore non-current quantity data
            if (naldData.IncrNo != quantitiesDataLine.AabvIncrNo ||
                naldData.IssueNo != quantitiesDataLine.AabvIssueNo)
            {
                continue;
            }

            naldData.MaxAnnualQty = quantitiesDataLine.MaxAnnualQty;
            naldData.MaxDailyQty = quantitiesDataLine.MaxDailyQty;
            naldData.QuantityAggregated = quantitiesDataLine.AggregatedInd;
            naldData.QuantityUserValid = quantitiesDataLine.UserValidInd;
            naldData.QuantityPurpPoints = quantitiesDataLine.PurpPointsInd switch
            {
                '1' => "Single Point / Single Purpose",
                '2' => "Single Point / Multiple Purposes",
                '3' => "Multiple Points / Single Purpose",
                '4' => "Multiple Points / Multiple Purposes",
                _ => "Unknown"
            };
        }
    }

    private static Dictionary<string, NaldData> AddNaldAbstractionLicencePurposeData(
        List<NaldLicencePurposeDataLine> naldLicencePurposeDataLines,
        HashSet<string> licenceNumbersNotInDataset,
        ref Dictionary<string, NaldData> generalNaldData)
    {
        var returnDict = new Dictionary<string, NaldData>();

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

            var naldDataPurpose = new NaldDataPurpose
            {
                Id = int.Parse(purposeDataLine.Id!),
                CategoryUse = new NaldDataPurposeCategoryUse
                {
                    PrimaryCategoryCode = purposeDataLine.ApurApprCode!,
                    PrimaryCategoryDescription = purposeDataLine.PurpPrimDescr!,
                    SecondaryCategoryCode = purposeDataLine.ApurApseCode!,
                    SecondaryCategoryDescription = purposeDataLine.PurpSecDescr!,
                    UseCode = purposeDataLine.ApurApusCode,
                    UseDescription = purposeDataLine.PurpUseDescr!,
                },
                Quantity = new NaldDataQuantity
                {
                    AnnualQty = double.TryParse(purposeDataLine.AnnualQty, out var annualQty) ? annualQty : null,
                    AnnualQtyUsability = purposeDataLine.AnnualQtyUsability,
                    DailyQty = double.TryParse(purposeDataLine.DailyQty, out var dailyQty) ? dailyQty : null,
                    DailyQtyUsability = purposeDataLine.DailyQtyUsability,
                    HourlyQty = double.TryParse(purposeDataLine.HourlyQty, out var hourlyQtt) ? hourlyQtt : null,
                    HourlyQtyUsability = purposeDataLine.HourlyQtyUsability,
                    InstQty = double.TryParse(purposeDataLine.InstQty, out var instQty) ? instQty : null,
                    InstQtyUsability = purposeDataLine.InstQtyUsability
                },
                Notes = purposeDataLine.Notes
            };

            returnDict.Add(purposeDataLine.PurposeIdLookupKey, naldData);
            naldData.Purposes.Add(naldDataPurpose);

            var period = new NaldDataPeriod
            {
                PurposeIds = [naldDataPurpose.Id],
                PeriodStartDay = purposeDataLine.PeriodStartDay,
                PeriodStartMonth = purposeDataLine.PeriodStartMonth,
                PeriodEndDay = purposeDataLine.PeriodEndDay,
                PeriodEndMonth = purposeDataLine.PeriodEndMonth
            };

            var existingPeriod = naldData.Periods.FirstOrDefault(x => x.ToString() == period.ToString());
            
            if (existingPeriod != null)
            {
                existingPeriod.PurposeIds.Add(naldDataPurpose.Id);
            }
            else
            {
                naldData.Periods.Add(period);
            }
        }

        return returnDict;
    }

    private static void AddNaldAbstractionLicencePointsData(
        List<NaldLicencePointDataLine> naldLicencePointDataLines,
        ref Dictionary<string, NaldData> purposeToLicenceMapping)
    {
        foreach (var pointDataLine in naldLicencePointDataLines)
        {
            if (!purposeToLicenceMapping.TryGetValue(pointDataLine.PurposeIdLookupKey, out var naldData))
            {
                // Just skip it - might be a purpose ID linked to a non-current version
                continue;
            }

            var purposeId = pointDataLine.PurposeId;

            var naldDataPurpose = naldData.Purposes.FirstOrDefault(x => x.Id == purposeId);
            if (naldDataPurpose == null)
            {
                // By definition, the NALD data discovered by the dictionary lookup should contain the purpose ID
                // matching the dictionary key, so we should never be able to hit this exception.
                throw new Exception("Purpose to licence mapping is corrupted");
            }

            var pointId = pointDataLine.PointId;
            naldDataPurpose.PointIds.Add(pointId);

            var naldDataPoint = naldData.Points.FirstOrDefault(x => x.PointId == pointId);
            if (naldDataPoint == null)
            {
                naldDataPoint = new NaldDataPoint
                {
                    PointId = pointDataLine.PointId,
                    PointName = pointDataLine.LocalName,
                    PurposeIds = [purposeId],
                    AaptAptpCode = pointDataLine.AaptAptpCode,
                    AaptAptsCode = pointDataLine.AaptAptsCode,
                    AapcCode = pointDataLine.AapcCode,
                    NationalGridReferences = [],
                    CartesianReferences = []
                };

                if (!string.IsNullOrWhiteSpace(pointDataLine.Ngr1Sheet) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr1East) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr1North))
                {
                    naldDataPoint.NationalGridReferences.Add(new NationalGridReference
                    {
                        ReferenceIndex = 1,
                        Sheet = pointDataLine.Ngr1Sheet,
                        East = pointDataLine.Ngr1East,
                        North = pointDataLine.Ngr1North
                    });
                }

                if (!string.IsNullOrWhiteSpace(pointDataLine.Ngr2Sheet) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr2East) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr2North))
                {
                    naldDataPoint.NationalGridReferences.Add(new NationalGridReference
                    {
                        ReferenceIndex = 2,
                        Sheet = pointDataLine.Ngr2Sheet,
                        East = pointDataLine.Ngr2East,
                        North = pointDataLine.Ngr2North
                    });
                }

                if (!string.IsNullOrWhiteSpace(pointDataLine.Ngr3Sheet) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr3East) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr3North))
                {
                    naldDataPoint.NationalGridReferences.Add(new NationalGridReference
                    {
                        ReferenceIndex = 3,
                        Sheet = pointDataLine.Ngr3Sheet,
                        East = pointDataLine.Ngr3East,
                        North = pointDataLine.Ngr3North
                    });
                }

                if (!string.IsNullOrWhiteSpace(pointDataLine.Ngr4Sheet) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr4East) ||
                    !string.IsNullOrWhiteSpace(pointDataLine.Ngr4North))
                {
                    naldDataPoint.NationalGridReferences.Add(new NationalGridReference
                    {
                        ReferenceIndex = 4,
                        Sheet = pointDataLine.Ngr4Sheet,
                        East = pointDataLine.Ngr4East,
                        North = pointDataLine.Ngr4North
                    });
                }

                if (pointDataLine.Cart1East.HasValue || pointDataLine.Cart1North.HasValue)
                {
                    naldDataPoint.CartesianReferences.Add(new CartesianReference
                    {
                        ReferenceIndex = 1,
                        East = pointDataLine.Cart1East,
                        North = pointDataLine.Cart1North
                    });
                }

                if (pointDataLine.Cart2East.HasValue || pointDataLine.Cart2North.HasValue)
                {
                    naldDataPoint.CartesianReferences.Add(new CartesianReference
                    {
                        ReferenceIndex = 2,
                        East = pointDataLine.Cart2East,
                        North = pointDataLine.Cart2North
                    });
                }

                if (pointDataLine.Cart3East.HasValue || pointDataLine.Cart3North.HasValue)
                {
                    naldDataPoint.CartesianReferences.Add(new CartesianReference
                    {
                        ReferenceIndex = 3,
                        East = pointDataLine.Cart3East,
                        North = pointDataLine.Cart3North
                    });
                }

                if (pointDataLine.Cart4East.HasValue || pointDataLine.Cart4North.HasValue)
                {
                    naldDataPoint.CartesianReferences.Add(new CartesianReference
                    {
                        ReferenceIndex = 4,
                        East = pointDataLine.Cart4East,
                        North = pointDataLine.Cart4North
                    });
                }

                naldData.Points.Add(naldDataPoint);
            }
            else
            {
                naldDataPoint.PurposeIds.Add(purposeId);
            }
        }
    }
}