using FakeItEasy;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

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

    public static (
        ICacheService CacheService,
        IAbstractionLicenceCacheService AbsLicCacheService,
        IAbstractionLicenceOutputService AbsLicOutputService)
        GetFakeCacheService(
            ICacheService realCacheService,
            IAbstractionLicenceCacheService realAbsLiceCacheService,
            IAbstractionLicenceOutputService realAbsLicenceOutputService,
            Dictionary<string, List<NaldAbstractionData>> naldAbstractionData,
            Dictionary<string, List<NaldImpoundmentData>> naldImpoundmentData,
            Dictionary<string, DmsFileData> dmsData)
    {
        var fakeCache = A.Fake<ICacheService>(x => x.Wrapping(realCacheService));
        var fakeCacheAbsLic = A.Fake<IAbstractionLicenceCacheService>(x => x.Wrapping(realAbsLiceCacheService));
        var fakeOutputAbsLic = A.Fake<IAbstractionLicenceOutputService>(x => x.Wrapping(realAbsLicenceOutputService));
        
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
            .CallTo(() => fakeCacheAbsLic.GetNaldAbstractionLicenceAsync(A<string>._, A<int>._))
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

                    if (!naldAbstractionData.TryGetValue(key, out var value))
                    {
                        continue;
                    }
                
                    var naldRow = (NaldAbstractionData?)value[0];
                    return Task.FromResult(naldRow);   
                }
                
                return Task.FromResult((NaldAbstractionData?)null);
            });
        
        A
            .CallTo(() => fakeCacheAbsLic.GetNaldImpoundmentLicenceAsync(A<string>._, A<int>._))
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

                    if (!naldImpoundmentData.TryGetValue(key, out var value))
                    {
                        continue;
                    }
                
                    var naldRow = (NaldImpoundmentData?)value[0];
                    return Task.FromResult(naldRow);   
                }
                
                return Task.FromResult((NaldImpoundmentData?)null);
            });

        return (fakeCache, fakeCacheAbsLic, fakeOutputAbsLic);
    }
}