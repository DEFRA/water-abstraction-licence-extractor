using System.Collections.Concurrent;
using System.Globalization;
using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WRADI.Services.LicenceFinder;
using WRADI.Services.LicenceFinder.Helpers;
using WRADI.Services.LicenceFinder.Implementations;
using WRADI.Services.LicenceFinder.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.Nald;
using FormattingHelper = WALE.ProcessFile.Core.Helpers.FormattingHelper;
using RegionHelper = WALE.ProcessFile.Core.Helpers.RegionHelper;

// Create a host builder with dependency injection
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        // Register all LicenceFinder services using the extension method
        services.AddLicenceFinderServices();
    })
    .Build();

// Get the service and call the method
using (var scope = host.Services.CreateScope())
{
    var licenceFileFinder = scope.ServiceProvider.GetRequiredService<ILicenceFileFinder>();
    var readExtractService = scope.ServiceProvider.GetRequiredService<IReadExtract>();

    var regionName = "Anglian Region";
    regionName = null;

    var restrictToRegionName = "North East";
    restrictToRegionName = null;

    var apiBaseUrl = "http://localhost:8080";
    //var apiBaseUrl = "https://wli-api-dev.aws-int.defra.cloud";
    //var apiBaseUrl = "https://wli-api-tst.aws-int.defra.cloud";

    //var flowToRun = "FindAllFilesToDownload";
    var flowToRun = "FindLicenceFilesAsync";
    //var flowToRun = "FindInspectionReportFiles";

    try
    {
        var generalApiClient = new GeneralApiClient(apiBaseUrl);
        
        // API - DMS data file export ~240k records (originally from Consolidate file)
        // dms_extract holds one extract per document type, so the flow decides which to read.
        // These must match the extractor's DocumentTypes.
        var extractType = flowToRun == "FindInspectionReportFiles"
            ? "WrInspectionReport"
            : "AbstractionLicence";

        var dmsRecords = await GetDmsExtractAsync(generalApiClient, extractType);
        var dmsRecordsData = GroupDmsRecords(dmsRecords.Data);

        switch (flowToRun)
        {
            case "FindLicenceFilesAsync":
            {
                // FLOW - Licence file finder (produces LicenceMatchResults_DATE.xlsx)
                Console.WriteLine("Starting licence file processing...");

                // API - NALD data - started early as async so we can run in parallel
                var naldDataTask = GetNaldDataAsync(apiBaseUrl);
                
                // API - DMS file id data (from what we've seen before)
                var dmsFileIdInformationTask = GetDmsFileIdInformationAsync(generalApiClient);
                
                // API - WRADI tool all local files inventory (from S3 stuff)
                var wradiAllLocalFilesInventoryTask = GetWradiPdfsInventoryFiles(apiBaseUrl);
                
                // API - WRADI tool file/licence reader (DOI, template type etc... scraping) extracts
                // (e.g. LicenceReader-yyyyMMdd.csv). Has date of issue, number of pages, template types etc...
                var wradiToolScrapeResultsTask = generalApiClient.GetDmsFileReaderResultsAsync();
                
                // API - Licence finder previous iteration run matches
                var licenceFinderLastIterationMatchesTask = GetLicenceFinderResultsAsync(generalApiClient);
                
                // Spreadsheet - DMS change audit overrides by our team (e.g. Overrides.xlsx)
                var dmsChangeAuditOverrides = readExtractService.GetDmsChangeAuditOverrides("Override_");
                
                // Spreadsheet - DMS manual fixes by our team/SamD (e.g. Manual_Fix_Extract.xlsx) - The 'Sam D' file
                // - doesn't often change
                var dmsManualFixes = readExtractService.GetDmsManualFixes();

                var (naldRecordsToProcess, naldAbsLicencesAndVersions, naldImportDate) = await naldDataTask;

                var licenceMatchResultsFilePath = await licenceFileFinder.FindLicenceFilesAsync(
                    dmsRecordsData,
                    dmsManualFixes,
                    dmsChangeAuditOverrides.Item1,
                    await dmsFileIdInformationTask,
                    generalApiClient,
                    naldRecordsToProcess,
                    naldAbsLicencesAndVersions,
                    await wradiToolScrapeResultsTask,
                    await licenceFinderLastIterationMatchesTask,
                    await wradiAllLocalFilesInventoryTask,
                    regionName,
                    dmsChangeAuditOverrides.Item2,
                    naldImportDate,
                    dmsRecords.ImportDate);

                Console.WriteLine($"Licence processing completed. Results saved to: {licenceMatchResultsFilePath}");
                break;
            }
            case "FindAllFilesToDownload":
            {
                 // FLOW - Find all files to download (i.e. all files, not just licences)
                 // NOTE - previously referred to as 'Build Version Download Info Excel'
                Console.WriteLine("Started finding all files to download...");

                // API - Licence finder previous iteration run matches
                var licenceFinderLastIterationMatchesTask = GetLicenceFinderResultsAsync(generalApiClient);
                
                // API - WRADI tool all local files inventory (from S3 stuff)
                var wradiAllLocalFilesInventoryTask = GetWradiPdfsInventoryFiles(apiBaseUrl);

                var result = await licenceFileFinder.FindAllFilesToDownloadAsync(
                    dmsRecordsData,
                    await licenceFinderLastIterationMatchesTask,
                    await wradiAllLocalFilesInventoryTask,
                    generalApiClient);

                Console.WriteLine($"File saved to {result}");
                break;
            }

            case "FindInspectionReportFiles":
            {
                // FLOW - Filter the DMS extract for inspection reports (WR51) by filename/folder
                // rules only
                Console.WriteLine("Started finding inspection report files...");

                // API - WRADI tool all local files inventory (from S3 stuff)
                var wradiAllLocalFilesInventoryTask = GetWradiPdfsInventoryFiles(apiBaseUrl);

                var inspectionReportFilePath = await licenceFileFinder.FindInspectionReportFilesAsync(
                    DmsDictionaryToList(dmsRecordsData),
                    generalApiClient,
                    await wradiAllLocalFilesInventoryTask);

                Console.WriteLine($"Inspection report files saved to: {inspectionReportFilePath}");
                break;
            }

            case "BuildFileTemplateIdentificationExtract":
            {
                // FLOW - Build file template identification extract - NOT REALLY USED ANYMORE (AUG 2026)
                Console.WriteLine("Started building file template identification extract...");

                // API - Licence finder previous iteration run matches
                var licenceFinderLastIterationMatchesTask = GetLicenceFinderResultsAsync(generalApiClient);
                
                // Spreadsheet - DMS change audit overrides by our team (e.g. Overrides.xlsx)
                var dmsChangeAuditOverrides = readExtractService.GetDmsChangeAuditOverrides("Override_");
                
                // Spreadsheet - File version results (e.g. LicenceVersionResults.xlsx) - Comes from JP
                var jpFileVersionResults = readExtractService.ReadFileVersionResultsFile();

                var resultFilePath = licenceFileFinder.BuildFileTemplateIdentificationExtract(
                    await licenceFinderLastIterationMatchesTask,
                    dmsChangeAuditOverrides.Item1,
                    jpFileVersionResults);

                Console.WriteLine($"File saved to {resultFilePath}");
                break;
            }
            case "FindLicenceFilesToDownload":
            {
                // FLOW - Find licence files to download (previously referred to as 'Build Download Info Excel')
                // NOTE 2026-May-22 I think FindLicenceFiles extra tabs supersede this NOT USED ANYMORE PROBABLY
                Console.WriteLine("Started finding licence files to download...");

                // API - Licence finder previous iteration run matches
                var licenceFinderLastIterationMatchesTask = GetLicenceFinderResultsAsync(generalApiClient);
                
                // API - WRADI tool all local files inventory (from S3 stuff)
                var wradiAllLocalFilesInventoryTask = GetWradiPdfsInventoryFiles(apiBaseUrl);

                var path = licenceFileFinder.FindLicenceFilesToDownload(
                    DmsDictionaryToList(dmsRecordsData),
                    await licenceFinderLastIterationMatchesTask,
                    await wradiAllLocalFilesInventoryTask,
                    restrictToRegionName);

                Console.WriteLine($"File saved to {path}");
                break;
            }
            case "FindLicenceFilesToDownload_SpreadsheetCompareOnly":
            {
                // FLOW - Find licence files to download (spreadsheet compare only - old way) NOT USED ANYMORE
                Console.WriteLine("Started finding licence files to download...");

                // API - Licence finder previous iteration run matches
                var licenceFinderLastIterationMatches = await GetLicenceFinderResultsAsync(generalApiClient);

                var fileName = licenceFileFinder.FindLicenceFilesToDownload_SpreadsheetCompareOnly(
                    DmsDictionaryToList(dmsRecordsData),
                    licenceFinderLastIterationMatches,
                    licenceFinderLastIterationMatches,
                    restrictToRegionName);

                Console.WriteLine($"File saved to {fileName}");
                break;
            }
            case "FindDuplicateLicenceFiles":
            {
                // FLOW - Find duplicate licence files (NOT USED ANYMORE - we read the files and check the hashes)
                var (naldRecordsToProcess, _, _) = await GetNaldDataAsync(apiBaseUrl);

                var duplicateFilePath = licenceFileFinder.FindDuplicateLicenceFiles(
                    DmsDictionaryToList(dmsRecordsData),
                    naldRecordsToProcess);

                Console.WriteLine($"Results saved to: {duplicateFilePath}");
                break;
            }
            default:
                throw new Exception($"Unknown flow: {flowToRun}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR - Error processing licence files: {ex}");
    }
}

await host.StopAsync();
return;

static async Task<List<LicenceMatchResult>> GetLicenceFinderResultsAsync(GeneralApiClient apiClient)
{
    var licenceFindResults = new List<LicenceMatchResult>();
    const int take = 10_000;

    List<LicenceMatchResult> licenceFinderResultsPartial = [];
    var loopIdx = 0;

    while (loopIdx == 0 || licenceFinderResultsPartial.Count == take)
    {
        var skip = take * loopIdx++;

        licenceFinderResultsPartial = await apiClient.GetLicenceFinderResultsAsync(skip, take);
        licenceFindResults.AddRange(licenceFinderResultsPartial);
    }

    return licenceFindResults;
}

static async Task<(List<DmsExtract> Data, string ImportDate)> GetDmsExtractAsync(
    GeneralApiClient generalApiClient,
    string extractType)
{
    var dmsExtractInfoRaw = new List<DmsExtract>();
    const int take = 10_000;

    List<DmsExtract> dmsExtractPartial = [];
    var loopIdx = 0;
    string? filename = null;

    while (loopIdx == 0 || dmsExtractPartial.Count == take)
    {
        var skip = take * loopIdx++;

        var (data, importDate) = await generalApiClient.GetDmsExtractAsync(skip, take, extractType);
        dmsExtractPartial = data;
        filename = importDate;

        dmsExtractInfoRaw.AddRange(dmsExtractPartial);
    }

    return (dmsExtractInfoRaw, filename)!;
}

static Dictionary<string, List<DmsExtract>> GroupDmsRecords(List<DmsExtract> dmsRecords)
{
    foreach (var dmsRecord in dmsRecords)
    {
        dmsRecord.PermitNumber = dmsRecord.PermitNumber.ToLower();
    }

    var groupedByPermit = dmsRecords.GroupBy(dr => dr.PermitNumber);

    return groupedByPermit.ToDictionary(
        grp => grp.Key,
        grp => grp.ToList());
}

static async Task<Dictionary<string, FileInventory>> GetWradiPdfsInventoryFiles(string apiBaseUrl)
{
    var inventoryApi = new InventoryApiClient(apiBaseUrl);

    var files = new List<FileMetadata>();
    var partialFiles = new List<FileMetadata>();

    const int take = 1_000;
    var loopIdx = 0;
    var startAfter = string.Empty;

    while (loopIdx == 0 || partialFiles.Count == take)
    {
        partialFiles = await inventoryApi.GetAllWithMetadataAsync(startAfter, take);
        files.AddRange(partialFiles);

        loopIdx += 1;
        startAfter = partialFiles.Last().Filename;
    }

    var returnDict = new Dictionary<string, FileInventory>(StringComparer.OrdinalIgnoreCase);

    foreach (var fileMetadata in files)
    {
        // Inspection report keys have three "__" parts where a licence key has two. Parsing only
        // the two-part shape left every WR51 file out of this inventory, so the delta reported all
        // of them as missing.
        var parsed = LicenceFileHelpers.ParseIngressFilename(fileMetadata.Filename);

        if (parsed == null)
        {
            continue;
        }

        var (permitNumber, fileId) = parsed.Value;

        if (returnDict.ContainsKey($"{permitNumber}_{fileId}"))
        {
            continue;
        }

        returnDict.Add($"{permitNumber}_{fileId}", new FileInventory
        {
            FolderName = "Api",
            FileName = fileMetadata.Filename,
            FileSize = fileMetadata.Filesize.ToString(),
            ModifiedTime = fileMetadata.ModifiedTime.ToString(CultureInfo.InvariantCulture),
            PermitNumber = permitNumber,
            FileId = fileId.ToString()
        });
    }

    return returnDict;
}


static List<DmsExtract> DmsDictionaryToList(Dictionary<string, List<DmsExtract>> dict)
{
    return dict
        .SelectMany(kvp => kvp.Value)
        .ToList();
}

static async Task<ConcurrentDictionary<Guid, List<DmsFileIdInformation>>>
    GetDmsFileIdInformationAsync(IGeneralApiClient generalApiClient)
{
    var dmsFileIdInformationList = await generalApiClient.GetDmsFileIdInformationAsync();
    var dmsFileIdInformationDict = new ConcurrentDictionary<Guid, List<DmsFileIdInformation>>();

    foreach (var dmsFileIdInformation in dmsFileIdInformationList)
    {
        if (!dmsFileIdInformationDict.TryGetValue(dmsFileIdInformation.FileId, out var changeList))
        {
            changeList = [];
            dmsFileIdInformationDict.TryAdd(dmsFileIdInformation.FileId, changeList);
        }

        changeList.Add(dmsFileIdInformation);
    }

    return dmsFileIdInformationDict;
}

static async Task<(List<
        NaldSimpleRecord> NaldSimpleRecords,
        Dictionary<string, List<NaldLicenceVersion>> NaldData,
        string ImportDate)>
    GetNaldDataAsync(string apiBaseUrl)
{
    var naldApiClient = new NaldApiClient(apiBaseUrl);

    var naldApiStatusDataTask = naldApiClient.GetNaldLicenceStatusDataAsync();

    const int take = 10_000;
    var allNaldData = new NaldDataCollection
    {
        AbstractionLicences = [],
        AbstractionLicenceVersions = []
    };

    var allNaldDataPartial = new NaldDataCollection();
    var loopIdx = 0;

    while (loopIdx == 0
        || allNaldDataPartial.AbstractionLicences!.Count == take
        || allNaldDataPartial.AbstractionLicenceVersions!.Count == take)
    {
        var skip = take * loopIdx++;

        allNaldDataPartial = await naldApiClient.GetNaldDataAsync(null, false, skip, take);
        allNaldData.AbstractionLicences!.AddRange(allNaldDataPartial.AbstractionLicences!);
        allNaldData.AbstractionLicenceVersions!.AddRange(allNaldDataPartial.AbstractionLicenceVersions!);
    }

    var naldApiStatusData = await naldApiStatusDataTask;

    var naldSimpleRecords = new List<NaldSimpleRecord>();
    var naldData = new Dictionary<string, List<NaldLicenceVersion>>();
    var naldVersionsDict = new Dictionary<string, List<NaldLicenceVersionDataLine>>();

    if (allNaldData.AbstractionLicenceVersions == null || allNaldData.AbstractionLicenceVersions.Count == 0)
    {
        throw new Exception("Nald Api licence versions came back empty");
    }

    // NOTE - LicenceVersions is only pulling back newest currently, so this is overkill
    foreach (var licenceVersion in allNaldData.AbstractionLicenceVersions)
    {
        if (!naldVersionsDict.ContainsKey(licenceVersion.LookupKey))
        {
            naldVersionsDict.Add(licenceVersion.LookupKey, []);
        }

        naldVersionsDict[licenceVersion.LookupKey].Add(licenceVersion);
    }

    foreach (var licence in allNaldData.AbstractionLicences!)
    {
        var licenceNumberWithoutSeperators = LicenceFileHelpers.CleanPermitNumber(licence.LicenceNo!);

        if (!naldData.ContainsKey(licenceNumberWithoutSeperators))
        {
            naldData.Add(licenceNumberWithoutSeperators, []);
        }

        var strippedNumber = FormattingHelper.StripForComparison(
            licence.LicenceNo!,
            licence.FgacRegionCode)!;

        var lookupKey = $"{licence.FgacRegionCode}|{licence.Id}";

        if (naldVersionsDict.TryGetValue(lookupKey, out var licenceVersions))
        {
            foreach (var version in licenceVersions)
            {
                naldData[licenceNumberWithoutSeperators].Add(
                    new NaldLicenceVersion
                    {
                        AablId = version.AablId?.ToString(),
                        AabvType = version.AabvType,
                        IssueNo = version.IssueNo.ToString(),
                        IncrementNo = version.IncrNo,
                        LicenceNumber = licence.LicenceNo!,
                        Region = licence.FgacRegionCode.ToString(),
                        SignatureDate = version.LicSigDate,
                        ArepEiucCode = licence.ArepEiucCode
                    });
            }
        }

        var isLive = naldApiStatusData.LiveLicences.Contains(strippedNumber);

        if (!isLive)
        {
            continue;
        }

        var naldSimpleRecord = new NaldSimpleRecord
        {
            LicNo = licence.LicenceNo!,
            DmsPermitNo = licenceNumberWithoutSeperators,
            Region = RegionHelper.GetRegionName(licence.FgacRegionCode)
        };

        naldSimpleRecords.Add(naldSimpleRecord);
    }

    var importDate = await naldApiClient.GetImportRunDateAsync("Nald");
    return (naldSimpleRecords, naldData, importDate ?? "Unknown");
}
