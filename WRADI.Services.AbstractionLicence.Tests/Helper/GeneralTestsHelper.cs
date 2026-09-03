using FakeItEasy;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public static class GeneralTestsHelper
{
    public static (ICacheService CacheService, IAbstractionLicenceCacheService AbsLicCacheService)
        GetFakeCacheService(
            ICacheService realCacheService,
            IAbstractionLicenceCacheService realAbsLiceCacheService,
            Dictionary<string, List<NaldAbstractionData>> naldData,
            Dictionary<string, DmsFileData> dmsData)
    {
        var fakeCache = A.Fake<ICacheService>(x => x.Wrapping(realCacheService));
        var fakeCacheAbsLic = A.Fake<IAbstractionLicenceCacheService>(x => x.Wrapping(realAbsLiceCacheService));
        
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

                    if (!naldData.TryGetValue(key, out var value))
                    {
                        continue;
                    }
                
                    var naldRow = (NaldAbstractionData?)value[0];
                    return Task.FromResult(naldRow);   
                }
                
                return Task.FromResult((NaldAbstractionData?)null);
            });

        return (fakeCache, fakeCacheAbsLic);
    }
}