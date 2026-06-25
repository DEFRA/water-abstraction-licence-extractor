using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Converters;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
namespace WALE.OrchestrateFileProcess.Services.Implementations;


public class OrchestrateFileProcessService(
    AppSettings settings,
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService,
    IOrchestratorService orchestratorService)
    : IOrchestrateFileProcess
{
    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        ConsoleHelper.WriteLine("INFO - OrchestrateFileProcessService - Started");

        if (settings.RefreshCache)
        {
            await cacheService.ClearCacheAsync();
        }
        
        await cacheService.SetupAsync();
        await outputService.SetupAsync();

        //const short regionCode = 3;

        var dtStartGetDms = DateTime.Now;
        ConsoleHelper.WriteLine("INFO - OrchestrateFileProcessService - Getting DMS files to process");

        var (dmsFilesToProcess, allDmsData) =
            await GetDmsFilesAndMappingAsync(settings.FileMappingPath!,
                false);

        var saveDuration = (DateTime.Now - dtStartGetDms).TotalMilliseconds;

        ConsoleHelper.WriteLine(
            $"INFO - OrchestrateFileProcessService - Got DMS files to process in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
       
        var processRun = await outputService.StartProcessRunAsync(new ProcessRun
        {
            Description = $"Batch Process Request for single file  process  from : {fileService!.FolderPath}",
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = dmsFilesToProcess.Count,
            Status = "Batch"
        });

        try
        {
            foreach (var (filePath, _) in dmsFilesToProcess)
            {
                await orchestratorService.AddToFileProcessQueue(new SingleFileProcessRequest
                {
                    FilePath = filePath,
                    ProcessRunId = processRun.ProcessRunId
                });
                ConsoleHelper.WriteLine($"INFO - {filePath} - sent to Process File Queue");
            }
        }
        catch (Exception e)
        {
            ConsoleHelper.WriteLine($"ERROR - OrchestrateFileProcessService - Error during sending to file processing queue: {e}");
            throw;
        }
        
        ConsoleHelper.WriteLine($"INFO - OrchestrateFileProcessService - Finished processing at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
               return true;
    }
    
    private async
        Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers,
            Dictionary<string, DmsFileData> LicenceNumbersWithFilenames)>
        GetDmsFilesAndMappingAsync(
            string dmsReportPath,
            bool getFromFile)
    {
        //var filesAndMapping = GetFilesAndMappingFromFolders(services.PdfFolderPath!);
        (Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
            LicenceNumbersWithFilenames) filesAndMapping;
    
        if (getFromFile)
        {
            filesAndMapping = await GetFilesAndMappingFromExcelDownloadInfoFileAsync(dmsReportPath);
        }
        else
        {
            filesAndMapping = await GetFilesAndMappingFromLicenceFinderResultsAsync();
        }
    
        /*filesAndMapping.FilepathsWithLicenceNumbers = filesAndMapping.FilepathsWithLicenceNumbers
            .Where(filePath => filePath.Key.Contains("22722086"))
            .ToDictionary(filePath => filePath.Key, k => k.Value);*/

        filesAndMapping.FilenamesWithLicenceNumbers = filesAndMapping.FilenamesWithLicenceNumbers
            .OrderBy(filePath => filePath.Key)
            //.Where(x => x.Key.Contains("12405035_")) // TODO This file is slow (3X slower then some others - work out why)
            //.Where(x => /*x.Key.Contains("12100063") || */ x.Key.Contains("12504175r01__bf7b7908-fa43-61ef-b29e-475502aa2f94"))
            .Where(x => x.Value.RegionId == 3) // North east
            //.Skip(155)
            //.Take(5)
            .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

        return filesAndMapping;
    }
    
    private async Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
        LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromLicenceFinderResultsAsync()
{
    var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
    var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

    var allDestinationFilenames = await fileService.GetAllFilesAsync();

    var lowercaseFilesInFolder = allDestinationFilenames.Select(f => f.ToLower()).ToHashSet();
    var licenceFinderResults = await cacheService.GetLicenceFinderResultsAsync(0, int.MaxValue);

    foreach (var licenceFinderResult in licenceFinderResults)
    {
        if (licenceFinderResult.FileId == null)
        {
            continue;
        }
        
        var destinationFileName = $"{licenceFinderResult.PermitNumber.ToLower()}__{licenceFinderResult.FileId!.ToLower()}.pdf";
        
        if (!lowercaseFilesInFolder.Contains(destinationFileName))
        {
            continue;
        }

        // Fix casing
        destinationFileName = allDestinationFilenames.First(fname =>
            fname.Equals(destinationFileName, StringComparison.CurrentCultureIgnoreCase));
        
        var regionId = RegionHelper.GetRegionId(licenceFinderResult.Region);
        
        var dmsFileData = new DmsFileData
        {
            DestinationFileName = destinationFileName,
            NaldLicenceRef = licenceFinderResult.LicenseNumber,
            PermitNumber = licenceFinderResult.PermitNumber,
            DmsPath = licenceFinderResult.FileUrl,
            StrippedLicenceNumber = FormattingHelper.StripForComparison(
                licenceFinderResult.LicenseNumber,
                regionId)!,
            FileId = Guid.Parse(licenceFinderResult.FileId!),
            RegionId = regionId
        };

        filenamesWithLicenceNumbers.Add(destinationFileName, dmsFileData);
        licenceNumbersWithFilenames.TryAdd(dmsFileData.StrippedLicenceNumber, dmsFileData);
    }
    
    return (
        filenamesWithLicenceNumbers,
        licenceNumbersWithFilenames
    );
}

    private async
        Task<(Dictionary<string, DmsFileData> FilenamesWithLicenceNumbers, Dictionary<string, DmsFileData>
            LicenceNumbersWithFilenames)>
        GetFilesAndMappingFromExcelDownloadInfoFileAsync(
            string dmsReportPath)
    {
        var filenamesWithLicenceNumbers = new Dictionary<string, DmsFileData>();
        var licenceNumbersWithFilenames = new Dictionary<string, DmsFileData>();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var filesInFolder = await fileService.GetAllFilesAsync();

        await using (var stream = File.Open(dmsReportPath, FileMode.Open, FileAccess.Read))
        {
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                if (dataSet.Tables.Count == 0)
                {
                    throw new InvalidOperationException("No worksheets found in the Excel file.");
                }

                var dataTable = dataSet.Tables[0];

                if (dataTable.Rows.Count == 0)
                {
                    throw new InvalidOperationException("Excel file is empty.");
                }

                foreach (DataRow row in dataTable.Rows)
                {
                    var permitNumberField = row["Permit Number"];
                    string permitNumber;

                    if (permitNumberField is string permitNumberValue)
                    {
                        permitNumber = permitNumberValue;
                    }
                    else
                    {
                        permitNumber = ((double)permitNumberField).ToString(CultureInfo.InvariantCulture);
                    }

                    string destinationFileName;
                    string dmsPath;
                    Guid fileId;

                    if (dataTable.Columns.Contains("Definitive URL"))
                    {
                        dmsPath = (string)row["Definitive URL"];
                        destinationFileName = (string)row[1];

                        var filenameParts = destinationFileName.Split("__");
                        fileId = filenameParts.Length >= 2
                            ? Guid.Parse(filenameParts[1])
                            : throw new Exception("Filename format was incorrect");
                    }
                    else
                    {
                        var fileIdColumn = row["File Id"] != DBNull.Value ? (string)row["File Id"] : null;

                        if (!Guid.TryParse(fileIdColumn, out fileId))
                        {
                            continue;
                        }

                        dmsPath = (string)row["File URL"];
                        destinationFileName = $"{permitNumber}_{fileId}.pdf";
                    }

                    if (!filesInFolder.Contains(destinationFileName))
                    {
                        continue;
                    }

                    var naldLicenceRef = (string)row["License Number"];

                    var dmsFileData = new DmsFileData
                    {
                        DestinationFileName = destinationFileName,
                        NaldLicenceRef = naldLicenceRef,
                        PermitNumber = permitNumber,
                        DmsPath = dmsPath,
                        StrippedLicenceNumber = FormattingHelper.StripForComparison(naldLicenceRef, -1)!,
                        FileId = fileId,
                        RegionId = GeneralConstants.GenericRegionCode
                    };

                    filenamesWithLicenceNumbers.Add(destinationFileName, dmsFileData);
                    licenceNumbersWithFilenames.Add(dmsFileData.StrippedLicenceNumber, dmsFileData);
                }
            }
        }

        return (
            filenamesWithLicenceNumbers,
            licenceNumbersWithFilenames
        );
    }
}