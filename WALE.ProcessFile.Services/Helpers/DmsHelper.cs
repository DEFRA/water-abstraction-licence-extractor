using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class DmsHelper
{
    public static ConcurrentDictionary<Guid, List<DmsFileIdInformation>> TranformDmsFileIdInformation(
        List<DmsFileIdInformation> dmsFileIdInformationList)
    {
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

    public static async Task<(DmsFileData DmsFileData, NaldLicence NaldLicence)>
        GetDmsAndNaldFileData(ICacheService cacheService, Guid fileId)
    {
        var licenceFinderResult = await cacheService.GetLicenceFinderResultAsync(fileId);
        return LicenceFinderResultToDmsAndNaldData(licenceFinderResult, null);
    }

    public static async Task<(Dictionary<string, (DmsFileData DmsFileData, NaldLicence NaldLicence)> FilenamesWithLicenceNumbers,
            Dictionary<string, (DmsFileData DmsFileData, NaldLicence NaldLicence)> LicenceNumbersWithFilenames)>
        GetDmsAndNaldFilesAndMappingAsync(
            IFileService fileService,
            string dmsReportPath,
            bool getFromFile,
            ICacheService cacheService)
    {
        var dtStartGetDms = DateTime.Now;
        
        //var filesAndMapping = GetFilesAndMappingFromFolders(services.PdfFolderPath!);
        (Dictionary<string, (DmsFileData, NaldLicence)> FilenamesWithLicenceNumbers, Dictionary<string, (DmsFileData, NaldLicence)>
            LicenceNumbersWithFilenames) filesAndMapping;
    
        if (getFromFile)
        {
            filesAndMapping = await GetFilesAndMappingFromExcelDownloadInfoFileAsync(
                fileService,
                dmsReportPath);
        }
        else
        {
            filesAndMapping = await GetFilesAndMappingFromLicenceFinderResultsAsync(
                fileService,
                cacheService);
        }
    
        filesAndMapping.FilenamesWithLicenceNumbers = filesAndMapping.FilenamesWithLicenceNumbers
            .OrderBy(filePath => filePath.Key)
            .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);

        // For debugging uncheck sections of the following
    
        filesAndMapping.FilenamesWithLicenceNumbers = filesAndMapping.FilenamesWithLicenceNumbers
            //.Where(x => x.Key.Contains("22722027", StringComparison.InvariantCultureIgnoreCase)
            //            || x.Key.Contains("1asdssdds", StringComparison.InvariantCultureIgnoreCase))
            //.Where(x => /*x.Key.Contains("12100063") || x.Key.Contains("12504175r01__bf7b7908-fa43-61ef-b29e-475502aa2f94"))
            //.Where(x => x.Value.RegionId == 3) // North east
            //.Skip(155)
            .Take(1)
            .ToDictionary(filePath => filePath.Key, filePath => filePath.Value);
    
        var saveDuration = (DateTime.Now - dtStartGetDms).TotalMilliseconds;

        ConsoleHelper.WriteLine(
            $"INFO - OrchestrateFileProcessService - Got DMS files to process in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return filesAndMapping;
    }
    
    private static async Task<(Dictionary<string, (DmsFileData, NaldLicence)> FilenamesWithLicenceNumbers, Dictionary<string, (DmsFileData, NaldLicence)> LicenceNumbersWithFilenames)>
        GetFilesAndMappingFromExcelDownloadInfoFileAsync(
            IFileService fileService,
            string dmsReportPath)
    {
        var filenamesWithLicenceNumbers = new Dictionary<string, (DmsFileData, NaldLicence)>();
        var licenceNumbersWithFilenames = new Dictionary<string, (DmsFileData, NaldLicence)>();

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
                        PermitNumber = permitNumber,
                        DmsPath = dmsPath,
                        FileId = fileId
                    };

                    var regionCode = -1; // TODO
                    
                    var naldLicence = new NaldLicence
                    {
                        LicenceNumber = permitNumber, // TODO
                        Id = -1,
                        RegionCode = (short)regionCode,
                        Type = LicenceType.Abstraction // TODO
                    };

                    var strippedLicenceNumber = FormattingHelper.StripForComparison(naldLicenceRef, regionCode)!;
                    
                    filenamesWithLicenceNumbers.Add(destinationFileName, (dmsFileData, naldLicence));
                    licenceNumbersWithFilenames.Add(strippedLicenceNumber, (dmsFileData, naldLicence));
                }
            }
        }

        return (
            filenamesWithLicenceNumbers,
            licenceNumbersWithFilenames
        );
    }
    
    private static async Task<(Dictionary<string, (DmsFileData, NaldLicence)> FilenamesWithLicenceNumbers, Dictionary<string, (DmsFileData, NaldLicence)>
        LicenceNumbersWithFilenames)>
    GetFilesAndMappingFromLicenceFinderResultsAsync(IFileService fileService, ICacheService cacheService)
    {
        var filenamesWithLicenceNumbers = new Dictionary<string, (DmsFileData, NaldLicence)>();
        var licenceNumbersWithFilenames = new Dictionary<string, (DmsFileData, NaldLicence)>();

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
            
            var (dmsFileData, naldLicence) = LicenceFinderResultToDmsAndNaldData(
                licenceFinderResult,
                destinationFileName);
            
            var strippedLicenceNumber = FormattingHelper.StripForComparison(
                licenceFinderResult.LicenseNumber,
                naldLicence.RegionCode)!;
            
            filenamesWithLicenceNumbers.Add(destinationFileName, (dmsFileData, naldLicence));
            licenceNumbersWithFilenames.TryAdd(strippedLicenceNumber, (dmsFileData, naldLicence));
        }
        
        return (
            filenamesWithLicenceNumbers,
            licenceNumbersWithFilenames
        );
    }

    private static (DmsFileData DmsFileData, NaldLicence NaldLicence) LicenceFinderResultToDmsAndNaldData(
        LicenceFinderResult licenceFinderResult,
        string? destinationFileName)
    {
        if (string.IsNullOrEmpty(destinationFileName))
        {
            destinationFileName = $"{licenceFinderResult.PermitNumber.ToLower()}__{licenceFinderResult.FileId!.ToLower()}.pdf";
        }
        
        var naldLicence = new NaldLicence
        {
            RegionCode = (short)RegionHelper.GetRegionId(licenceFinderResult.Region),
            LicenceNumber = licenceFinderResult.LicenseNumber,
            Id = -1, // TODO
            Type = LicenceType.Abstraction // TODO
        };
        
        var dmsFileData = new DmsFileData
        {
            DestinationFileName = destinationFileName,
            PermitNumber = licenceFinderResult.PermitNumber,
            DmsPath = licenceFinderResult.FileUrl,
            FileId = Guid.Parse(licenceFinderResult.FileId!)
        };

        return (dmsFileData, naldLicence);
    }
}