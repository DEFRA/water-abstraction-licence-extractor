using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Services;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.DocumentType.WrInspectionReport.Configuration;

/// The one construction of a WR51 LookupConfiguration. The pipeline and the accuracy harness both
/// build through here: when they built their own, they drifted onto different label sets and
/// different layout values, and the harness scored a pipeline nobody ran.
public static class WrInspectionReportLookupConfigurationFactory
{
    /// <param name="tableExtractorService">
    /// Serves both the structured and unstructured roles. Required: labels in this set read table
    /// free text, and the extractor throws when they have none.
    /// </param>
    public static LookupConfiguration Build(
        IFileService fileService,
        ICacheService cacheService,
        IOutputService outputService,
        ITableExtractorService tableExtractorService,
        int regionId,
        DateTime requestedAt,
        int lockRetryCount = 0)
    {
        var configuration = new LookupConfiguration(
            WrInspectionReportTextBasedLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            new NullLicenceNumberService(),
            tableExtractorService,
            tableExtractorService,
            new DmsLookupService(),
            regionId,
            requestedAt,
            lockRetryCount,
            lineHeight: 6,
            minimumRowsForDigital: 30);

        return configuration;
    }
}
