using FakeItEasy;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Tests.Helper;

public static class GeneralTestsHelper
{
    public static List<LabelGroupResult> ExcludeSomeMatches(List<LabelGroupResult> matches)
    {
        return matches
            .Where(m =>
                m.LabelGroupName != "LinkedLicenceNumber"
                && m.LabelGroupName != "SourceOfSupply"
                && m.LabelGroupName != "ReasonsForConditions"
                && m.LabelGroupName != "ScheduleOfConditionsA"
                && m.LabelGroupName != "ScheduleOfConditionsB")
            .ToList();
        }

    public static ICacheService GetFakeCacheService(
        ICacheService realCacheService,
        Dictionary<string, List<NaldData>> naldData,
        Dictionary<string, DmsFileData> dmsData)
    {
        var fakeCache = A.Fake<ICacheService>(x => x.Wrapping(realCacheService));
        
        A
            .CallTo(() => fakeCache.GetDmsFileDataAsync(A<string>._))
            .ReturnsLazily(x =>
            {
                var licenceNumberInNaldFormat = (string)x.Arguments[0]!;
                
                var strippedLicenceNumbers = FormattingHelper.StripForComparisonMultipleOptions(
                    licenceNumberInNaldFormat,
                    GeneralConstants.UnsetRegionCode);

                foreach (var strippedLicenceNumber in strippedLicenceNumbers)
                {
                    if (!dmsData.TryGetValue(strippedLicenceNumber, out var value))
                    {
                        continue;
                    }
                
                    var dmsFileData = (DmsFileData?)value;
                    return Task.FromResult(dmsFileData);   
                }
                
                return Task.FromResult((DmsFileData?)null);
            });
        
        A
            .CallTo(() => fakeCache.GetNaldLicenceAsync(A<string>._, A<int>._))
            .ReturnsLazily(x =>
            {
                var licenceNumberInNaldFormat = (string)x.Arguments[0]!;
                var regionCode = (int)x.Arguments[1]!;
                
                var strippedLicenceNumbers = FormattingHelper.StripForComparisonMultipleOptions(
                    licenceNumberInNaldFormat,
                    regionCode);

                foreach (var strippedLicenceNumber in strippedLicenceNumbers)
                {
                    var key = $"{regionCode}|{strippedLicenceNumber}";

                    if (!naldData.TryGetValue(key, out var value))
                    {
                        continue;
                    }
                
                    var naldRow = (NaldData?)value[0];
                    return Task.FromResult(naldRow);   
                }
                
                return Task.FromResult((NaldData?)null);
            });

        return fakeCache;
    }
}