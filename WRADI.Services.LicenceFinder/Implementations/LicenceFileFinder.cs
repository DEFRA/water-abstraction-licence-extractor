using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.Nald;
using WRADI.Services.LicenceFinder.Helpers;
using WRADI.Services.LicenceFinder.Models;
using DmsFileIdInformation = WALE.ProcessFile.Core.Models.Dms.DmsFileIdInformation;
using DmsFileReaderResult = WALE.ProcessFile.Core.Models.Dms.DmsFileReaderResult;
using DmsManualFixExtract = WALE.ProcessFile.Core.Models.Dms.DmsManualFixExtract;

namespace WRADI.Services.LicenceFinder.Implementations;

/// <inheritdoc/>
public class LicenceFileFinder : ILicenceFileFinder
{
    private readonly ILicenceFileProcessor _fileProcessor;
    private readonly List<ILicenceMatchingRule> _rulesToMatch;

    /// <summary>
    /// Common header mapping for LicenseMatchResult - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> LicenseMatchResultHeaderMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "FileUrl", "File URL" },
        { "RuleUsed", "Rule Used" },
        { "ChangeAuditAction", "Override Action" },
        { "LicenseNumber", "License Number" },
        { "PrimaryTemplate", "Primary Template" },
        { "SecondaryTemplate", "Secondary Template" },
        { "NumberOfPages", "Number Of Pages" },
        { "DocumentDate", "Document Date" },
        { "SignatureDate", "Latest issued signature date" },
        { "NaldId", "NALD AABL_ID" },
        { "NaldIssueNo", "NALD Issue_No" },
        { "NaldIncrementNo", "NALD Increment_No" },        
        { "DateOfIssue", "Scrapped Date of Issue" },
        { "DOISignatureDateMatch", "Latest issued signature date = Scraped Date of Issue" },
        { "IncludedInVersionMatch", "Included in VersionMatch process" },
        { "SingleLicenceInVersionMatch", "Single Licence found in VersionMatch process" },
        { "VersionMatchFileUrl", "Version Match Licence URL" },
        { "DuplicateLicenceInVersionMatchResult", "Duplicate licences found in VersionMatch process" },
        { "NaldIssue", "Signature date DQ issue found in VersionMatch process" },
        { "OtherReference", "Other Reference" },
        { "FileSize", "File Size" },
        { "DisclosureStatus", "Disclosure Status" },
        { "Region", "Region" },
        { "PreviousIterationRuleUsed", "Previous Iteration Rule Used" },
        { "DifferenceInRuleusedInIterations", "Difference In Rule Used In Iterations" },
        { "PreviousIterationFileUrl", "Previous Iteration File URL" },
        { "DifferenceInFileUrlInIterations", "Difference In File URL In Iterations" },
        { "FileId", "File ID" },
        { "FileIdStatus", "File ID Status" },
        { "FileIdStatusChangeDate", "File ID Status Change Date" },
        { "IsWaterCompany", "Is Water Company" },
        { "FolderNameAutoCorrect", "Folder Name Auto Correct" },
        { "SeenInDmsExtract", "Seen In Dms Extract"},
        { "WeHaveDownloaded", "We Have Downloaded"}
    };
    
    /// <summary>
    /// Common header mapping for UnmatchedLicenseMatchResult - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> UnmatchedLicenseMatchResultHeaderMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "LicenseNumber", "License Number" },
        { "Region", "Region" },
        { "FileUrl", "File URL" },
        { "SignatureDateOfFileEvaluated", "Signature Date Of File Evaluated" },
        { "FileEvaluated", "File Evaluated" },
        { "FileTypeEvaluated", "Evaluated File Type" },
        { "FileDeterminedAsLicence", "File Determined As Licence" },
        { "LicenceCount", "Licence Count For Permit Number"},
        { "NALDDataQualityIssue", "Is NALD Data Quality Issue" },
        { "NaldID", "Nald Id" },
        { "NaldIssueNo", "NALD Issue No." },
        { "NaldIncrementNo", "NALD Increment No." },
        { "DateOfIssueOfEvaluatedFile", "Date of Issue Of Evaluated File" },
        { "OriginalFileUrlIdentifiedAsLicence", "Original File URL Identified As Licence"},
        { "FileId", "File ID" },
        { "FileIdStatus", "File ID Status" },
        { "FileIdStatusChangeDate", "File ID Status Change Date" }
    };
    
    /// <summary>
    /// Common header mapping for delta tab - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> DeltaMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "FileUrl", "File URL" }
    };
    
    /// <summary>
    /// Common header mapping for DMS tab - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> DmsMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "FileName", "Filename" },
        { "FileId", "File Id" }
    };
    
    /// <summary>
    /// Common header mapping for Nald tab - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> NaldMapping = new()
    {
        { "LicNo", "Licence Number" },
        { "DmsPermitNo", "(Assumed) Dms Permit No" },
        { "Region", "Region" }
    };
    
    /// <summary>
    /// Common header mapping for s3 files tab - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> S3FilesMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "FileId", "File Id" },
        { "FileName", "Filename" },
        { "FolderName", "Folder Name" },
        { "FileSize", "File Size" },
        { "ModifiedTime", "Uploaded Date/Time" }
    };
    
    /// <summary>
    /// Common header mapping for overrides tab - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, string> OverrideMapping = new()
    {
        { "PermitNumber", "Permit Number" },
        { "FileUrl", "File Url" },
        { "IssueNo", "Issue No" },
        { "IncrementNo", "Increment No" },
        { "FileId", "File Id" },
    };

    public LicenceFileFinder(
        ILicenceFileProcessor fileProcessor,
        IEnumerable<ILicenceMatchingRule> rulesToMatch)
    {
        _fileProcessor = fileProcessor ?? throw new ArgumentNullException(nameof(fileProcessor));

        ArgumentNullException.ThrowIfNull(rulesToMatch);

        // Sort by priority to ensure correct rule execution order
        _rulesToMatch = rulesToMatch
            .OrderBy(r => r.Priority)
            .ToList();

        if (_rulesToMatch.Count == 0)
        {
            throw new ArgumentException("At least one rule must be provided", nameof(rulesToMatch));
        }
    }

    public async Task<string> FindLicenceFilesAsync(
        Dictionary<string, List<WALE.ProcessFile.Core.Models.Dms.DmsExtract>> dmsRecords,
        Dictionary<string, DmsManualFixExtract> dmsManualFixes,
        List<Override> dmsChangeAuditOverrides,
        ConcurrentDictionary<Guid, List<DmsFileIdInformation>> dmsFileIdInformation,
        IGeneralApiClient generalApiClient,
        List<NaldSimpleRecord> naldRecordsToProcess,
        Dictionary<string, List<NaldLicenceVersion>> naldData,
        List<DmsFileReaderResult> wradiToolScrapeResults,
        List<LicenceMatchResult> licenceFinderPreviousIterationMatches,
        Dictionary<string, FileInventory> wradiLocalFilesInventory,
        string? regionName,
        string overridesFilename,
        string naldDate,
        string dmsDate)
    {
        try
        {
            // Process each NALD record and find matches using rules
            var (
                    licenceMatchResults,
                    unmatchedLicenceMatchResults,
                    deltaResults)
                = await ProcessLicenceMatchingAsync(
                    dmsRecords,
                    dmsManualFixes,
                    dmsChangeAuditOverrides,
                    dmsFileIdInformation,
                    generalApiClient,
                    naldRecordsToProcess,
                    naldData,
                    wradiToolScrapeResults,
                    licenceFinderPreviousIterationMatches,
                    wradiLocalFilesInventory);

            // Save to DB
            await generalApiClient.ClearLicenceFinderResultsAsync();
            const int chunkSize = 1_000;
            
            var chunks = licenceMatchResults.Chunk(chunkSize);

            foreach (var chunk in chunks)
            {
                await generalApiClient.SaveLicenceFinderResultsAsync(chunk.ToList());
            }
            
            // Generate output Excel file
            var worksheetData = new List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)>
            {
                ("Match Results", LicenseMatchResultHeaderMapping, licenceMatchResults),
                ("Version Results", UnmatchedLicenseMatchResultHeaderMapping, unmatchedLicenceMatchResults),
                ("Files Needed Locally (Delta)", DeltaMapping, deltaResults),
                ($"DMS Info ({dmsDate})", DmsMapping, dmsRecords
                    .SelectMany(x => x.Value)
                    .Select(x => new
                    {
                        x.FileName,
                        x.FileId,
                        x.PermitNumber
                    })
                    .Take(0)),
                ($"NALD Info ({naldDate})", NaldMapping, naldRecordsToProcess),
                ($"Overrides ({overridesFilename})", OverrideMapping, dmsChangeAuditOverrides),
                ("Local files", S3FilesMapping, wradiLocalFilesInventory.Select(x => x.Value))
            };
            
            var outputFileName = $"LicenceMatchResults_{DateTime.Now:yyyyMMdd_HHmmss}";
            var firstFile = _fileProcessor.GenerateExcel(worksheetData, outputFileName);

            if (string.IsNullOrEmpty(regionName))
            {
                return firstFile;
            }

            // OPTIONAL - Filter to only 1 region
            licenceMatchResults = licenceMatchResults
                .Where(lmr => lmr.Region == regionName)
                .ToList();

            unmatchedLicenceMatchResults = unmatchedLicenceMatchResults
                .Where(lmr => lmr.Region == regionName)
                .ToList();

            // Generate output Excel file
            worksheetData =
            [
                ("Match Results", LicenseMatchResultHeaderMapping, licenceMatchResults),
                ("Version Results", UnmatchedLicenseMatchResultHeaderMapping, unmatchedLicenceMatchResults)
            ];

            outputFileName =
                $"{regionName.Replace(" ", string.Empty)}Only_LicenceMatchResults_{DateTime.Now:yyyyMMdd_HHmmss}";

            _fileProcessor.GenerateExcel(worksheetData, outputFileName);
            return firstFile;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while finding licence files: {ex.Message}", ex);
        }
    }

    public string FindLicenceFilesToDownload_SpreadsheetCompareOnly(
        List<WALE.ProcessFile.Core.Models.Dms.DmsExtract> dmsRecords,
        List<LicenceMatchResult> prevMatches,
        List<LicenceMatchResult> currentMatches,
        string? filterRegion = null)
    {
        // Foreach permit number in the newest created file (that was also included on the previous version of that file),
        // check if the permit number row has a fileId that is different to the previous run. If so fetch the DMS info
        // for that file and include it in files to download
        
        var filteredPrevMatches = prevMatches;

        if (!string.IsNullOrWhiteSpace(filterRegion))
        {
            filteredPrevMatches = prevMatches
                .Where(pm => pm.Region.Equals(filterRegion, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        // Create a set of valid permit numbers from filtered prevMatches
        var previouslySeenPermitNumbers = new HashSet<string>(
            filteredPrevMatches.Select(pm => pm.PermitNumber),
            StringComparer.OrdinalIgnoreCase
        );

        // Filter current matches to only include permit numbers that exist in prevMatches
        var currentMatchesSeenPreviously = currentMatches
            .Where(c => previouslySeenPermitNumbers.Contains(c.PermitNumber))
            .ToList();
        
        var missingFileDmsRecords = new List<(WALE.ProcessFile.Core.Models.Dms.DmsExtract DmsExtract, string FileId, string Reason)>();

        foreach (var currentMatchSeenPreviously in currentMatchesSeenPreviously)
        {
            try
            {
                var matchedFileId = filteredPrevMatches.FirstOrDefault(pm =>
                    pm.FileId?.Equals(currentMatchSeenPreviously.FileId, StringComparison.OrdinalIgnoreCase) == true);

                // Seen this file id before, so we don't want it
                if (matchedFileId != null)
                {
                    continue;
                }
                
                var missingFileDmsRecord = dmsRecords.FirstOrDefault(dms => dms.FileId == currentMatchSeenPreviously.FileId);

                if (missingFileDmsRecord == null)
                {
                    var dmsPermitMatches = dmsRecords
                        .Where(dms => dms.PermitNumber == currentMatchSeenPreviously.PermitNumber)
                        .ToList();

                    var dmsFileUrlMatch = dmsPermitMatches.FirstOrDefault(dms => dms.FileUrl == currentMatchSeenPreviously.FileUrl);

                    if (dmsFileUrlMatch == null)
                    {
                        Console.WriteLine($"WARNING - FileId {currentMatchSeenPreviously.FileId} not found in" +
                            $" DMS current extract. {dmsPermitMatches.Count} matches found in DMS on permit number.");   
                        
                        continue;
                    }

                    var d = new DateTime(1900, 1, 1);
                    d = d.AddDays(Convert.ToDouble(dmsFileUrlMatch.ModifiedDate));
                    
                    Console.WriteLine(
                        $"INFO - File URL '{currentMatchSeenPreviously.FileUrl}' matches." +
                        $" Current DMS file id is '{dmsFileUrlMatch.FileId}, ours was '{currentMatchSeenPreviously.FileId}'." +
                        $" File changed on '{d.ToShortDateString()}'");

                    var isOverride = currentMatchSeenPreviously.RuleUsed == "Override";

                    if (isOverride)
                    {
                        Console.WriteLine("Is override so skipping");
                        continue;
                    }
                    
                    // No matching permit number + filename in DMS records, include this file
                    missingFileDmsRecords.Add((dmsFileUrlMatch, dmsFileUrlMatch.FileId, $"FileId changed (Not an override, from {currentMatchSeenPreviously.FileId!}"));
                    
                    continue;
                }
                
                // No matching permit number + filename in DMS records, include this file
                missingFileDmsRecords.Add((missingFileDmsRecord, currentMatchSeenPreviously.FileId!, "TODO"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - {ex.Message}");
                throw;
            }
        }

        // Build download info records
        var downloadInfoRecords = new List<DownloadInfoOriginal>();
        
        try
        {
            downloadInfoRecords.AddRange(
                missingFileDmsRecords.Select(file => new DownloadInfoOriginal
                {
                    PermitNumber = file.DmsExtract.PermitNumber,
                    FullPath = file.DmsExtract.FileUrl,
                    SitePath = ExtractSitePath(file.DmsExtract.FileUrl),
                    LibraryAndFilePath = ExtractLibraryAndFilePath(file.DmsExtract.FileUrl),
                    FileId = file.FileId,
                    Reason = file.Reason
                }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {ex.Message}");
            throw;
        }

        // Create Excel output with specified column headers
        var headerMapping = new Dictionary<string, string>
        {
            { "PermitNumber", "PermitNumber" },
            { "FullPath", "FullPath" },
            { "SitePath", "SitePath" },
            { "LibraryAndFilePath", "LibraryAndFilePath" },
            { "FileId", "FileId" },
            { "Reason", "Reason" }
        };

        var outputFileName = _fileProcessor.GenerateExcel(
            downloadInfoRecords,
            $"Download_Info_{DateTime.Now:yyyyMMdd_HHmmss}",
            headerMapping);
        
        return outputFileName;
    }
    
    public async Task<string> FindAllFilesToDownloadAsync(
        Dictionary<string, List<DmsExtract>> dmsRecords,
        List<LicenceMatchResult> currentMatches,
        Dictionary<string, FileInventory> wradiAllLocalFilesInventory,
        IGeneralApiClient apiClient)
    {
        // Foreach permit number in the newest created file, find the DMS records for that permit number.
        // Check if the permit number row has a fileId that is different to the previous run. If so fetch the DMS info
        // for that file and include it in files to download
        
        Console.WriteLine("Getting filtered current matches");
        
        var filteredCurrentMatches = currentMatches
            .Where(c => !c.DoiSignatureDateMatch
                && !string.IsNullOrEmpty(c.SignatureDate)
                && !c.ChangeAuditAction.Equals("Override", StringComparison.CurrentCultureIgnoreCase)
                && c.RuleUsed != "Not Applicable") // Folder not found
            .ToList();

        // Filter current match by region if specified, otherwise use all
        /*if (!string.IsNullOrWhiteSpace(filterRegion))
        {
            filteredCurrentMatches = filteredCurrentMatches
                .Where(pm =>
                    pm.Region.Equals(filterRegion, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }*/

        Console.WriteLine($"Got {filteredCurrentMatches.Count} filtered current matches");
        
        var missingFiles = new List<DmsExtract>();
        var allFiles = new List<DmsExtract>();

        var permitNumberRegions = new Dictionary<string, int>();

        var idx = 0;
        
        foreach (var filteredCurrentMatch in filteredCurrentMatches)
        {
            if (idx++ % 1000 == 0)
            {
                Console.WriteLine($"Processing record {idx - 1}");
            }
            
            var permitNumber = filteredCurrentMatch.PermitNumber.ToLower();
            
            // Find all files in DMS records that match the permit number of the current match
            var dmsRecordsForPermitNumber = dmsRecords.TryGetValue(
                permitNumber,
                out var records) ? records : [];

            if (!dmsRecordsForPermitNumber.Any())
            {
                continue;
            }

            permitNumberRegions.Add(permitNumber, RegionHelper.GetRegionId(filteredCurrentMatch.Region));
            allFiles.AddRange(dmsRecordsForPermitNumber);
            
            // Find missing files (i.e. that are NOT in allFilesInventory)
            var filesNotInInventory = dmsRecordsForPermitNumber
                .Where(dmsRecordForPermitNumber =>
                {
                    var exists = wradiAllLocalFilesInventory.Any(inventoryRecord =>
                        inventoryRecord.Value.PermitNumber?
                            .Equals(dmsRecordForPermitNumber.PermitNumber, StringComparison.CurrentCultureIgnoreCase) == true
                        && inventoryRecord.Value.FileId?
                            .Equals(dmsRecordForPermitNumber.FileId, StringComparison.InvariantCultureIgnoreCase) == true);
                    
                    return !exists;
                })
                .ToList();

            // Add all files not in inventory to missing files
            missingFiles.AddRange(filesNotInInventory);
        }
        
        Console.WriteLine("Getting mising records");

        var missingRecords = missingFiles
            .Select(file => new DownloadInfoMissing
            {
                PermitNumber = file.PermitNumber,
                FullPath = file.FileUrl,
                SitePath = ExtractSitePath(file.FileUrl),
                LibraryAndFilePath = ExtractLibraryAndFilePath(file.FileUrl)
            })
            .ToList();
        
        Console.WriteLine("Getting all records");
        
        var allRecords = allFiles
            .Select(file =>
            {
                int fileSize;

                if (file.FileSize.EndsWith(" KB"))
                {
                    fileSize = Convert.ToInt32(double.Parse(file.FileSize.Replace(" KB", string.Empty)) * 1024.0);
                }
                else if (file.FileSize.EndsWith(" MB"))
                {
                    fileSize = Convert.ToInt32(double.Parse(file.FileSize.Replace(" MB", string.Empty)) * 1024.0 * 1024.0);
                }
                else if (file.FileSize.EndsWith(" B"))
                {
                    fileSize = Convert.ToInt32(double.Parse(file.FileSize.Replace(" B", string.Empty)));
                }
                else
                {
                    fileSize = int.Parse(file.FileSize);
                }
                
                return new DownloadInfoAll
                {
                    PermitNumber = file.PermitNumber,
                    FullPath = file.FileUrl,
                    SitePath = ExtractSitePath(file.FileUrl),
                    LibraryAndFilePath = ExtractLibraryAndFilePath(file.FileUrl),
                    RegionId = permitNumberRegions[file.PermitNumber.ToLower()],
                    FileId = Guid.Parse(file.FileId),
                    FileName = file.FileName,
                    FileSize = fileSize
                };
            })
            .ToList();

        var missingHeaderMapping = new Dictionary<string, string>
        {
            { "PermitNumber", "PermitNumber" },
            { "FullPath", "FullPath" },
            { "SitePath", "SitePath" },
            { "LibraryAndFilePath", "LibraryAndFilePath" }
        };
        
        var allHeaderMapping = new Dictionary<string, string>
        {
            { "PermitNumber", "PermitNumber" },
            { "FullPath", "FullPath" },
            { "SitePath", "SitePath" },
            { "LibraryAndFilePath", "LibraryAndFilePath" },
            { "RegionId", "RegionId" },
            { "FileId", "FileId" },
            { "FileName", "FileName" },
            { "FileSize", "FileSize" }
        };
        
        var worksheetData = new List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)>
        {
            ("Missing", missingHeaderMapping, missingRecords),
            ("All", allHeaderMapping, allRecords)
        };

        
        Console.WriteLine("Starting saving version files");
        
        var saveVersionAllTask = SaveVersionFilesAsync(apiClient, allRecords);
        await SaveVersionFilesToDownloadAsync(apiClient, missingRecords);

        await saveVersionAllTask;
        
        // NOTE! 2026-May-21 - The number we get for missing is less then the number JP calculated - we need to
        // look at this in the future to see where the difference in logic is

        return _fileProcessor.GenerateExcel(
            worksheetData,
            $"Version_Download_Info_{DateTime.Now:yyyyMMdd_HHmmss}");
    }

    private static async Task SaveVersionFilesAsync(
        IGeneralApiClient apiClient,
        List<DownloadInfoAll> results)
    {
        Console.WriteLine("ClearVersionFilesAsync started");
        await apiClient.ClearVersionFilesAsync();
        Console.WriteLine("ClearVersionFilesAsync finished");
        
        const int chunkSize = 10_000;
        var chunks = results.Chunk(chunkSize).ToList();
        var idx = 1;
        
        foreach (var chunk in chunks)
        {
            await apiClient.SaveVersionFilesAsync(chunk.ToList());    
            Console.WriteLine($"SaveVersionFilesAsync - Chunk {idx++} of {chunks.Count}");
        }
    }
    
    private static async Task SaveVersionFilesToDownloadAsync(
        IGeneralApiClient apiClient,
        List<DownloadInfoMissing> missingRecords)
    {
        Console.WriteLine("ClearVersionFilesToDownloadAsync started");
        await apiClient.ClearVersionFilesToDownloadAsync();
        Console.WriteLine("ClearVersionFilesToDownloadAsync started");
        
        const int chunkSize = 10_000;
        var chunks = missingRecords.Chunk(chunkSize).ToList();
        var idx = 1;

        foreach (var chunk in chunks)
        {
            await apiClient.SaveVersionFilesToDownloadAsync(chunk.ToList());
            Console.WriteLine($"SaveVersionFilesToDownloadAsync - Chunk {idx++} of {chunks.Count}");
        }
    }

    public string FindLicenceFilesToDownload(
        List<DmsExtract> dmsRecords,
        List<LicenceMatchResult> currentMatches,
        Dictionary<string, FileInventory> wradiAllLocalFilesInventory,
        string? filterRegion = null)
    {
        // Foreach permit number in the newest created file, find the DMS records for that file url.
        // Then fetch the inventory records for that permit number. If we don't have a file with the right
        // filename ([permit_number]__[file_id]__[filename]) we add it to the report
        
        var filteredCurrentMatches = currentMatches
            .Where(c => !c.DoiSignatureDateMatch
                && !c.ChangeAuditAction.Contains("Override", StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        // Filter current match by region if specified, otherwise use all
        if (!string.IsNullOrWhiteSpace(filterRegion))
        {
            filteredCurrentMatches = filteredCurrentMatches
                .Where(pm =>
                    pm.Region.Equals(filterRegion, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Files that should be included in the report
        var missingFiles = new List<DmsExtract>();
        
        foreach (var filteredCurrentMatch in filteredCurrentMatches)
        {
            try
            {
                // Find all files in DMS records that match the permit number of the current match
                var dmsRecordsForFileUrl = dmsRecords
                    .Where(dmsRecord => dmsRecord.FileUrl == filteredCurrentMatch.FileUrl)
                    .ToList();

                var inventoryRecordsForPermitNumber = wradiAllLocalFilesInventory
                    .Where(fileInventory => fileInventory.Value.PermitNumber == filteredCurrentMatch.PermitNumber)
                    .ToList();
                
                // Find files that are NOT in allFilesInventory
                var filesNotInInventory = dmsRecordsForFileUrl
                    .Where(dmsRecordForPermitNumber =>
                    {
                        var exists = inventoryRecordsForPermitNumber.Any(inventoryRecord =>
                        {
                            // Extract filename after first occurrence of "__" from inventory record
                            var fileNameParts = inventoryRecord.Value.FileName.Split("__");
                            
                            var extractedFileName =
                                fileNameParts.Length > 1 ? fileNameParts.Last() : inventoryRecord.Value.FileName;

                            return inventoryRecord.Value.FileId == dmsRecordForPermitNumber.FileId
                                && extractedFileName.Equals(dmsRecordForPermitNumber.FileName);
                        });
                        
                        return !exists;
                    })
                    .ToList();

                // Add all files not in inventory to missing files
                missingFiles.AddRange(filesNotInInventory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - {ex.Message}");
                throw;
            }
        }

        // Build download info records
        var downloadInfoRecords = new List<DownloadInfoOriginal>();
        
        try
        {
            downloadInfoRecords.AddRange(
                missingFiles.Select(file => new DownloadInfoOriginal
                {
                    PermitNumber = file.PermitNumber,
                    FullPath = file.FileUrl,
                    SitePath = ExtractSitePath(file.FileUrl),
                    LibraryAndFilePath = ExtractLibraryAndFilePath(file.FileUrl),
                    FileId = file.FileId,
                    Reason = "Don't have locally"
                }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {ex.Message}");
            throw;
        }

        // Create Excel output with specified column headers
        var headerMappingMissing = new Dictionary<string, string>
        {
            { "PermitNumber", "PermitNumber" },
            { "FullPath", "FullPath" },
            { "SitePath", "SitePath" },
            { "LibraryAndFilePath", "LibraryAndFilePath" },
            { "OriginalFileName", "OriginalFileName" },
            { "DestinationFileName__1", "DestinationFileName__1" },
            { "FileId", "FileId" },
            { "Reason", "Reason" }
        };
        
        // Create Excel output with specified column headers
        var headerMappingHave = new Dictionary<string, string>
        {
            { "FolderName", "FolderName" },
            { "PermitNumber", "PermitNumber" },
            { "FileId", "FileId" },
            { "FileName", "FileName" },
            { "FileSize", "FileSizeBytes" },
            { "ModifiedTime", "ModifiedTime" }
        };

        var worksheetData = new List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)>
        {
            ("Missing", headerMappingMissing, downloadInfoRecords),
            ("Have", headerMappingHave, wradiAllLocalFilesInventory)
        };
        
        var outputFileName = _fileProcessor.GenerateExcel(
            worksheetData,
            $"Download_Info_{DateTime.Now:yyyyMMdd_HHmmss}");

        return outputFileName;
    }

    /// <summary>
    /// Extracts the site path from a file URL (part before 'lib' occurrence, case-insensitive)
    /// </summary>
    private static string ExtractSitePath(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return string.Empty;

        var libIndex = fileUrl.IndexOf("lib", StringComparison.OrdinalIgnoreCase);

        if (libIndex > 0)
        {
            return fileUrl.Substring(0, libIndex);
        }

        return fileUrl;
    }

    /// <summary>
    /// Extracts the library and file path from a file URL (part from 'lib' occurrence till end, case-insensitive)
    /// </summary>
    private static string ExtractLibraryAndFilePath(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return string.Empty;

        var libIndex = fileUrl.IndexOf("lib", StringComparison.OrdinalIgnoreCase);

        if (libIndex >= 0)
        {
            return fileUrl.Substring(libIndex);
        }

        return string.Empty;
    }

    public string FindDuplicateLicenceFiles(List<DmsExtract> dmsRecords, List<NaldSimpleRecord> naldRecords)
    {
        try
        {
            // Step 1: Receive all DMS extract files
            
            // Step 2: Process duplicate detection
            var results = ProcessDuplicateDetection(dmsRecords, naldRecords);

            // Step 3: Generate output Excel file
            var outputFileName = $"DuplicateResults_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            return _fileProcessor.GenerateExcel(results, outputFileName, new Dictionary<string, string>()
            {
                { "PermitNumber", "Permit Number" },
                { "FileUrl", "File URL" },
                { "FileName", "File Name" },
                { "Region", "Region" }
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while finding licence files: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The only signal strong enough to admit a file on its own. The Inspection pattern below is
    /// much weaker and is subject to <see cref="InspectionReportNoiseTerms"/>.
    /// </summary>
    private static readonly string[] InspectionReportWr51TokenPatterns = ["WR51", "WR 51", "WR-51", "WR_51"];

    private const string InspectionReportInspectionPattern = "Inspection";
    private const string InspectionReportComplianceFolderSegment = "/Compliance/";

    /// <summary>
    /// Words naming a different kind of document. Applied to everything except an explicit WR51
    /// token. "email" is deliberately absent: it also matches a genuine report.
    /// </summary>
    private static readonly string[] InspectionReportNoiseTerms =
    [
        "certificate", "test", "photo", "calibration", "supporting information", "readings",
        "records"
    ];

    /// <summary>
    /// The Compliance folder alone is too weak a signal, so a file reaching this branch must also
    /// read like a report. "insp" covers "insp report" and the "Inspetion" misspelling; "insep" is
    /// separate because "insepction" transposes the letters and "insp" does not match it.
    /// </summary>
    private static readonly string[] InspectionReportComplianceIncludeTerms =
        ["report", "water resources", "site visit", "insp", "insep"];

    /// <summary>Applied after the include terms, so a WR51-token file is unaffected.</summary>
    private static readonly string[] InspectionReportComplianceExcludeTerms = ["Letter"];

    /// <summary>
    /// Hands off Flow, a separate workstream. Word-bounded: a bare "HOF" also matches the surname
    /// in "Mr and Mrs Hoff Post Insp signed".
    /// </summary>
    private static readonly Regex HandsOffFlow =
        new(@"\bhof\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The WR54 site visit form, spelled the four ways WR51 is. Separate from
    /// <see cref="OtherWrFormNumber"/>, which ignores separators. "FormWR54" contains "WR54".
    /// </summary>
    private static readonly string[] OtherWrFormTerms = ["WR54", "WR 54", "WR-54", "WR_54"];

    /// <summary>
    /// Any other WR form number written without a separator: WR16, WR32, WR52, WR53, bare
    /// "wr26.pdf". A separator is not allowed because the digits are then usually a date or a
    /// licence number ("WR 15.08.17", "WR 13 43 025 S 114").
    /// </summary>
    private static readonly string[] PdfExtensions = [".pdf"];

    private static readonly string[] WordExtensions = [".doc", ".docx"];

    private static readonly Regex OtherWrFormNumber =
        new(@"\bwr(?!51\b)\d{2}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static bool IsInspectionReportFile(DmsExtract dmsRecord) =>
        IsInspectionReportFile(dmsRecord, PdfExtensions);

    private static bool IsInspectionReportFile(DmsExtract dmsRecord, string[] allowedExtensions)
    {
        if (!dmsRecord.Regime.Equals("WRL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!allowedExtensions.Any(extension =>
                dmsRecord.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Before the includes: WR54 inspection reports otherwise arrive on the "Inspection"
        // pattern, where a later exclusion never sees them.
        if (NamesAnotherWrForm(dmsRecord.FileName))
        {
            return false;
        }

        if (InspectionReportWr51TokenPatterns.Any(pattern =>
                dmsRecord.FileName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Before both weaker branches: the folder branch's "insp" include term is a substring of
        // "inspection", so a file rejected here would otherwise be readmitted.
        if (InspectionReportNoiseTerms.Any(term =>
                dmsRecord.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (dmsRecord.FileName.Contains(
                InspectionReportInspectionPattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!dmsRecord.FileUrl.Contains(InspectionReportComplianceFolderSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!InspectionReportComplianceIncludeTerms.Any(term =>
                dmsRecord.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (InspectionReportComplianceExcludeTerms.Any(term =>
                dmsRecord.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !HandsOffFlow.IsMatch(dmsRecord.FileName);
    }

    private static bool NamesAnotherWrForm(string fileName) =>
        OtherWrFormTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
        || OtherWrFormNumber.IsMatch(fileName);

    /// <inheritdoc/>
    public async Task<string> FindInspectionReportFilesAsync(
        List<DmsExtract> dmsRecords,
        IGeneralApiClient generalApiClient,
        Dictionary<string, FileInventory> wradiLocalFilesInventory)
    {
        try
        {
            var results = dmsRecords.Where(IsInspectionReportFile).ToList();

            Console.WriteLine($"Found {results.Count} inspection report files out of {dmsRecords.Count} DMS records.");

            var wordResults = dmsRecords
                .Where(record => IsInspectionReportFile(record, WordExtensions))
                .ToList();

            // Word files are saved too, so the table is the full record of what discovery found.
            // Nothing can read them yet - FileProcessOrchestrationService skips them by file name,
            // so do not remove them from here to keep them out of a run.
            await generalApiClient.ClearInspectionReportFinderResultsAsync();
            const int chunkSize = 1_000;

            foreach (var chunk in results.Concat(wordResults).Chunk(chunkSize))
            {
                await generalApiClient.SaveInspectionReportFinderResultsAsync(chunk.ToList());
            }

            // Delta - matched files not yet in the local/S3 inventory
            var deltaResults = results
                .Where(r => !wradiLocalFilesInventory.ContainsKey($"{r.PermitNumber.ToLower()}_{r.FileId}"))
                .Select(r => new DeltaResult
                {
                    PermitNumber = r.PermitNumber,
                    FileUrl = r.FileUrl
                })
                .ToList();

            Console.WriteLine(
                $"{deltaResults.Count} of {results.Count} inspection report files are not yet in the local/S3 inventory.");

            var wordDeltaResults = wordResults
                .Where(r => !wradiLocalFilesInventory.ContainsKey($"{r.PermitNumber.ToLower()}_{r.FileId}"))
                .ToList();

            Console.WriteLine(
                $"Found {wordResults.Count} Word-format inspection report files, "
                + $"{wordDeltaResults.Count} of them not yet in the local/S3 inventory.");

            var outputFileName = $"InspectionReportFiles_{DateTime.Now:yyyyMMdd_HHmmss}";

            // File Id as well as the URL: the ingress key is built from the permit number and the
            // file id, so whoever uploads these needs both to name them.
            var wordMapping = new Dictionary<string, string>
            {
                { "PermitNumber", "Permit Number" },
                { "FileName", "File Name" },
                { "FileUrl", "File URL" },
                { "FileId", "File Id" }
            };

            var worksheetData = new List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)>
            {
                ("Match Results", new Dictionary<string, string>
                {
                    { "PermitNumber", "Permit Number" },
                    { "FileUrl", "File URL" },
                    { "FileName", "File Name" },
                    { "LibraryName", "Library Name" },
                    { "Regime", "Regime" }
                }, results),
                ("Files Needed Locally (Delta)", DeltaMapping, deltaResults),
                ("Word Files Needed Locally", wordMapping, wordDeltaResults),
                ("Word Files (All)", wordMapping, wordResults)
            };

            return _fileProcessor.GenerateExcel(worksheetData, outputFileName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while finding inspection report files: {ex.Message}", ex);
        }
    }

    private async Task<List<UnmatchedLicenceMatchResult>> FindUnmatchedLicenceFilesAsync(
        Dictionary<string, List<DmsExtract>> dmsRecords,
        Dictionary<string, List<NaldLicenceVersion>> naldData,
        List<LicenceMatchResult> licenceFinderPreviousIterationMatches,
        List<DmsFileReaderResult> wradiToolScrapeResults,
        ConcurrentDictionary<Guid, List<DmsFileIdInformation>> dmsFileIdInformation,
        IGeneralApiClient generalApiClient)
    {
        // (Step 0: Receive previous iteration matches files (from licenceFinderPreviousIterationMatches))
        
        // Step 1: From previous matches file find records who have date of issue but date of issue isn't equal to signature date
        var recordsWithDifferentDates = licenceFinderPreviousIterationMatches
            .Where(previousRecord => !string.IsNullOrWhiteSpace(previousRecord.DateOfIssue)
                && !string.IsNullOrWhiteSpace(previousRecord.SignatureDate)
                && !previousRecord.DateOfIssue.Equals(previousRecord.SignatureDate, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"Found {recordsWithDifferentDates.Count} records where Date of Issue differs from Signature Date.");
        
        var unmatchedList = new List<UnmatchedLicenceMatchResult>();
        UnmatchedLicenceMatchResult? naldIssueResult;
        string? licenceFileIdStr;
        NaldLicenceVersion? matchedNaldRecords;
        bool? isWaterCompany;
        string fileId = string.Empty;
        string fileUrl = string.Empty;
        DmsFileIdInformation? fileIdInfo;
        
        foreach (var recordWithDifferentDate in recordsWithDifferentDates)
        {
            var permitNumber = recordWithDifferentDate.PermitNumber;
            var lowercasePermitNumber = permitNumber.ToLower();
            
            Console.WriteLine($"Record with permit number {permitNumber} has Date of " +
                $"Issue: {recordWithDifferentDate.DateOfIssue} and Signature Date: {recordWithDifferentDate.SignatureDate}");
            
            // Step 2: Read DMS extract files for permit number
            var dmsRecordsForPermit =
                dmsRecords.TryGetValue(lowercasePermitNumber, out var dmsRecordTemp)
                    ? dmsRecordTemp
                    : [];
            
            // Step 3: Read NALD extract files for permit number and type ISSUE
            var naldRecordsForPermit =
                naldData.GetValueOrDefault(permitNumber) ?? [];
            
            // Step 4: Find files from fileIdentificationExtract whose file name matches the
            // dmsRecordsForPermit File name and is of Type Licence or Addendum
            var allMatchedFiles = wradiToolScrapeResults
                .Where(scrapeResult => scrapeResult.PermitNumber?.Equals(permitNumber, StringComparison.OrdinalIgnoreCase) == true)
                .Where(scrapeResult => dmsRecordsForPermit.Any(dms => dms.FileName.Equals(scrapeResult.OriginalFileName, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            
            Console.WriteLine($"Found {allMatchedFiles.Count} matching identification files " +
                $"for permit {permitNumber}");
            
            var matchedLicenceOrAddendumFiles = allMatchedFiles
                .Where(file => file.FileType!.Equals("Licence", StringComparison.OrdinalIgnoreCase) || 
                   file.FileType.Equals("Addendum", StringComparison.OrdinalIgnoreCase)
                 )
                .OrderByDescending(fie => fie.DateOfIssue)
                .ToList();
            
            Console.WriteLine($"Found {matchedLicenceOrAddendumFiles.Count} matching licence or addendum" +
                $" identification files for permit {permitNumber}");

            // Step 5: Look for Licence files first
            var dateMatchedLicenceFiles = matchedLicenceOrAddendumFiles
                .Where(f =>
                {
                    if (f.FileType?.Equals("Licence", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        return false;
                    }

                    var signatureDate = DateTime.TryParse(recordWithDifferentDate.SignatureDate, out var sd)
                        ? sd
                        : (DateTime?)null;

                    return f.DateOfIssue == signatureDate;
                })
                .ToList();

            if (dateMatchedLicenceFiles.Any())
            {
                Console.WriteLine($"  Found {dateMatchedLicenceFiles.Count} Licence file(s) - processing complete");
                
                foreach (var matchedLicence in dateMatchedLicenceFiles)
                {
                    matchedNaldRecords = naldRecordsForPermit
                        .FirstOrDefault(naldRecord => naldRecord.SignatureDate == matchedLicence.DateOfIssue);

                    isWaterCompany = naldRecordsForPermit.FirstOrDefault() != null
                        ? naldRecordsForPermit.First().ArepEiucCode?.EndsWith("SWC",
                            StringComparison.InvariantCultureIgnoreCase) == true
                        : null;
                    
                    // Check for NALD data issues
                    naldIssueResult = CheckForNaldDataIssue(
                        matchedNaldRecords,
                        naldRecordsForPermit,
                        recordWithDifferentDate,
                        dmsRecordsForPermit,
                        matchedLicence,
                        allMatchedFiles);
                    
                    if (naldIssueResult != null)
                    {
                        unmatchedList.Add(naldIssueResult);
                        continue; // Skip adding the normal record if there's a NALD data issue
                    }

                    licenceFileIdStr = matchedLicence.FileId.ToString();
                    
                    fileId = dmsRecordsForPermit
                        .FirstOrDefault(d => d.FileId.Equals(licenceFileIdStr, StringComparison.OrdinalIgnoreCase))
                        ?.FileId ?? string.Empty;

                    fileUrl = dmsRecordsForPermit
                        .FirstOrDefault(d => d.FileId.Equals(licenceFileIdStr, StringComparison.OrdinalIgnoreCase))
                        ?.FileUrl ?? string.Empty;
                    
                    fileIdInfo = await RecordFileIdAsync(
                        fileId,
                        fileUrl,
                        dmsFileIdInformation,
                        generalApiClient);

                    unmatchedList.Add(new UnmatchedLicenceMatchResult
                    {
                        PermitNumber = recordWithDifferentDate.PermitNumber,
                        FileUrl = fileUrl,
                        SignatureDateOfFileEvaluated = matchedNaldRecords?.SignatureDate?.ToString("dd/MM/yyyy"),
                        LicenseNumber = recordWithDifferentDate.LicenseNumber,
                        Region = recordWithDifferentDate.Region,
                        FileEvaluated = matchedLicence.FileName,
                        FileTypeEvaluated = matchedLicence.FileType,
                        FileDeterminedAsLicence = true,
                        DateOfIssueOfEvaluatedFile = matchedLicence.DateOfIssue?.ToString("dd/MM/yyyy"),
                        NaldId = int.Parse(matchedNaldRecords?.AablId ?? "0"),
                        NaldIssueNo = int.Parse(matchedNaldRecords?.IssueNo ?? "0"),
                        NaldIncrementNo = matchedNaldRecords?.IncrementNo,
                        OriginalFileUrlIdentifiedAsLicence = recordWithDifferentDate.FileUrl,
                        FileId = fileId,
                        FileIdStatus = fileIdInfo?.Status,
                        FileIdStatusChangeDate = fileIdInfo?.StatusDateUtc.ToString("dd/MM/yyyy"),
                        IsWaterCompany = isWaterCompany
                    });
                }
                
                continue;
            }
        
            // Step 6: If no date matched licence files, look for Addendum and find corresponding Licence
            var dateMatchedAddendums = matchedLicenceOrAddendumFiles
                .Where(f =>
                {
                    if (f.FileType?.Equals("Addendum", StringComparison.OrdinalIgnoreCase) != true)
                    {
                        return false;
                    }
                    
                    var signatureDate = DateTime.TryParse(recordWithDifferentDate.SignatureDate, out var sd)
                        ? sd
                        : (DateTime?)null;

                    return f.DateOfIssue == signatureDate;
                })
                .DistinctBy(f => f.FileName)
                .ToList();
            
            if (!dateMatchedAddendums.Any())
            {
                continue;
            }
            
            foreach (var addendum in dateMatchedAddendums)
            {
                matchedNaldRecords = naldRecordsForPermit.FirstOrDefault(d =>
                    d.SignatureDate == addendum.DateOfIssue);

                isWaterCompany = naldRecordsForPermit.FirstOrDefault() != null
                    ? naldRecordsForPermit.First().ArepEiucCode?.EndsWith("SWC",
                        StringComparison.InvariantCultureIgnoreCase) == true
                    : null;
                
                var addendumFileIdStr = addendum.FileId.ToString();
                
                fileId = dmsRecordsForPermit
                    .FirstOrDefault(d => d.FileId.Equals(addendumFileIdStr, StringComparison.OrdinalIgnoreCase))
                    ?.FileId ?? string.Empty;
                
                fileUrl = dmsRecordsForPermit
                    .FirstOrDefault(d => d.FileId.Equals(addendumFileIdStr, StringComparison.OrdinalIgnoreCase))
                    ?.FileUrl ?? string.Empty;
                
                fileIdInfo = await RecordFileIdAsync(
                    fileId,
                    fileUrl,
                    dmsFileIdInformation,
                    generalApiClient);
                
                unmatchedList.Add(new UnmatchedLicenceMatchResult
                {
                    PermitNumber = recordWithDifferentDate.PermitNumber,
                    FileUrl = fileUrl,
                    LicenseNumber = recordWithDifferentDate.LicenseNumber,
                    SignatureDateOfFileEvaluated = matchedNaldRecords?.SignatureDate?.ToString("dd/MM/yyyy"),
                    Region = recordWithDifferentDate.Region,
                    FileEvaluated = addendum.FileName,
                    FileTypeEvaluated = addendum.FileType,
                    FileDeterminedAsLicence = false,
                    DateOfIssueOfEvaluatedFile = addendum.DateOfIssue?.ToString("dd/MM/yyyy"),
                    NaldId = int.Parse(matchedNaldRecords?.AablId ?? "0"),
                    NaldIssueNo = int.Parse(matchedNaldRecords?.IssueNo ?? "0"),
                    NaldIncrementNo = matchedNaldRecords?.IncrementNo,
                    FileId = fileId,
                    FileIdStatus = fileIdInfo?.Status,
                    FileIdStatusChangeDate = fileIdInfo?.StatusDateUtc.ToString("dd/MM/yyyy"),
                    IsWaterCompany = isWaterCompany
                });
            }

            Console.WriteLine($"  Found {dateMatchedAddendums.Count} Addendum file(s) - searching for Licence files with date <= {recordWithDifferentDate.SignatureDate}");
            
            var firstLicenceBeforeSignatureDate = matchedLicenceOrAddendumFiles
                .Where(f => f.FileType?.Equals("Licence", StringComparison.OrdinalIgnoreCase) == true &&
                    f.DateOfIssue != null &&
                    DateTime.TryParse(recordWithDifferentDate.SignatureDate, out var signatureDate) &&
                    f.DateOfIssue <= signatureDate)
                .OrderByDescending(f => f.DateOfIssue!)
                .FirstOrDefault();

            if (firstLicenceBeforeSignatureDate == null)
            {
                Console.WriteLine("    No corresponding Licence file found");
                continue;
            }

            matchedNaldRecords = naldRecordsForPermit.FirstOrDefault(d =>
                d.SignatureDate == firstLicenceBeforeSignatureDate.DateOfIssue);

            isWaterCompany = naldRecordsForPermit.FirstOrDefault() != null
                ? naldRecordsForPermit.First().ArepEiucCode?.EndsWith("SWC",
                    StringComparison.InvariantCultureIgnoreCase) == true
                : null;

            // Check for NALD data issues
            naldIssueResult = CheckForNaldDataIssue(
                matchedNaldRecords,
                naldRecordsForPermit,
                recordWithDifferentDate,
                dmsRecordsForPermit,
                firstLicenceBeforeSignatureDate,
                allMatchedFiles);

            //TODO morning Fri why not getting by here
            
            if (naldIssueResult != null)
            {
                unmatchedList.Add(naldIssueResult);
                continue; // Skip adding the normal record if there's a NALD data issue
            }

            Console.WriteLine(
                $"    Found corresponding Licence: {firstLicenceBeforeSignatureDate.FileName} (Date: {firstLicenceBeforeSignatureDate.DateOfIssue})");

            licenceFileIdStr = firstLicenceBeforeSignatureDate.FileId.ToString();
            
            fileId = dmsRecordsForPermit
                .FirstOrDefault(d => d.FileId.Equals(licenceFileIdStr, StringComparison.OrdinalIgnoreCase))
                ?.FileId ?? string.Empty;

            fileUrl = dmsRecordsForPermit
                .FirstOrDefault(d => d.FileId.Equals(licenceFileIdStr, StringComparison.OrdinalIgnoreCase))
                ?.FileUrl ?? string.Empty;

            fileIdInfo = await RecordFileIdAsync(
                fileId,
                fileUrl,
                dmsFileIdInformation,
                generalApiClient);

            unmatchedList.Add(new UnmatchedLicenceMatchResult
            {
                PermitNumber = recordWithDifferentDate.PermitNumber,
                FileUrl = fileUrl,
                LicenseNumber = recordWithDifferentDate.LicenseNumber,
                SignatureDateOfFileEvaluated = naldRecordsForPermit
                    .FirstOrDefault(d => d.SignatureDate == firstLicenceBeforeSignatureDate.DateOfIssue)?
                    .SignatureDate?
                    .ToString("dd/MM/yyyy") ?? string.Empty,
                Region = recordWithDifferentDate.Region,
                FileEvaluated = firstLicenceBeforeSignatureDate.FileName,
                FileTypeEvaluated = firstLicenceBeforeSignatureDate.FileType,
                FileDeterminedAsLicence = true,
                DateOfIssueOfEvaluatedFile = firstLicenceBeforeSignatureDate.DateOfIssue?.ToString("dd/MM/yyyy"),
                NaldId = int.Parse(matchedNaldRecords?.AablId ?? "0"),
                NaldIssueNo = int.Parse(matchedNaldRecords?.IssueNo ?? "0"),
                NaldIncrementNo = matchedNaldRecords?.IncrementNo,
                OriginalFileUrlIdentifiedAsLicence = recordWithDifferentDate.FileUrl,
                FileId = fileId,
                FileIdStatus = fileIdInfo?.Status,
                FileIdStatusChangeDate = fileIdInfo?.StatusDateUtc.ToString("dd/MM/yyyy"),
                IsWaterCompany = isWaterCompany
            });
        }

        // Calculate licence count for each permit number
        var orderedUnmatchedResults = unmatchedList
            .DistinctBy(f => f.FileId)
            .OrderBy(p => p.PermitNumber)
            .ToList();

        // Group by permit number to calculate licence counts
        var licenceCountByPermit = orderedUnmatchedResults
            .Where(r => r.FileDeterminedAsLicence
                && r.FileTypeEvaluated?.Equals("Licence", StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(r => r.PermitNumber)
            .ToDictionary(
                g => g.Key,
                g => g.Count());

        // Set licence count for each record
        foreach (var unmatchedRecord in orderedUnmatchedResults)
        {
            unmatchedRecord.LicenceCount = licenceCountByPermit.GetValueOrDefault(unmatchedRecord.PermitNumber, 0);
        }

        return orderedUnmatchedResults;
    }

    public string BuildFileTemplateIdentificationExtract(
        List<LicenceMatchResult> previousIterationMatches,
        List<Override> overrides,
        List<UnmatchedLicenceMatchResult> fileVersionResults)
    {
        var passResults = new List<string>
        {
            "12100004",
            "12100052",
            "12100065",
            "12201021",
            "12202043",
            "12203017",
            "12203108",
            "12205021",
            "12206055",
            "12301004",
            "12301067",
            "12303142",
            "12305029",
            "12401075",
            "12401076",
            "12403015",
            "12405035",
            "12501028",
            "12501033",
            "12502109",
            "12504008",
            "12504107",
            "12504118",
            "12504124",
            "12504125",
            "12504128",
            "12504136",
            "12504138",
            "12504141",
            "22631018",
            "22631131",
            "22632003",
            "22632122",
            "22632370",
            "22634080",
            "22634140A",
            "22701015",
            "22702013",
            "22706035",
            "22706087R01",
            "22707039",
            "22708052",
            "22708083",
            "22708137R01",
            "22709026",
            "22709081",
            "22709120",
            "22709178R01",
            "22709197R01",
            "22709198R01",
            "22710063",
            "22711092",
            "22711156",
            "22711167",
            "22712041",
            "22712163",
            "22713080",
            "22713121",
            "22713200",
            "22713211R01",
            "22715147",
            "22715238",
            "22715293",
            "22716022",
            "22717070",
            "22717171",
            "22718078",
            "22718079",
            "22718129R01",
            "22718132R01",
            "22718141R01",
            "22719130",
            "22719172",
            "22719174",
            "22719175",
            "22719188",
            "22719190",
            "22719197",
            "22720082",
            "22721099",
            "22721251",
            "22721360R01",
            "22721361R01",
            "22722010",
            "22722093",
            "22722174",
            "22722509",
            "22722555R01",
            "22723125",
            "22723389",
            "22723671R01",
            "22723716R01",
            "22723727R01",
            "22724199",
            "22724416R01",
            "22724428",
            "22725023",
            "22725061",
            "22725079",
            "22725136",
            "22725206",
            "22728260R01",
            "22728266R01",
            "22728285",
            "22728290R01",
            "NE0220001003",
            "NE0220002007",
            "NE0220002009",
            "NE0220003012",
            "NE0230001007",
            "NE0250002029",
            "NE0250003004",
            "NE0260031021R01",
            "NE0260032053",
            "NE0270006016",
            "NE0270007008",
            "NE0270009030",
            "NE0270011021",
            "NE0270011023",
            "NE0270011024",
            "NE0270011025",
            "NE0270011026",
            "NE0270011027",
            "NE0270011028",
            "NE0270012035",
            "NE0270012037",
            "NE0270012038",
            "NE0270012039",
            "NE0270012040",
            "NE0270012041",
            "NE0270012042",
            "NE0270012043",
            "NE0270012044",
            "NE0270012045",
            "NE0270012046",
            "NE0270012048",
            "NE0270013011",
            "NE0270013012",
            "NE0270013027",
            "NE0270015016",
            "NE0270015020",
            "NE0270015021",
            "NE0270016003",
            "NE0270016020",
            "NE0270016022",
            "NE0270016023",
            "NE0270018014",
            "NE0270018026",
            "NE0270023046",
            "NE0270023047",
            "NE0270024049",
            "NE0270024065",
            "NE0270027025"
        };
        
        // Step 1: Read all PreviousExtract
        var prevMatchResults = previousIterationMatches
            .Where(d => !string.IsNullOrWhiteSpace(d.DateOfIssue)
                && d.DateOfIssue.Equals(d.SignatureDate,  StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        // Step 2: Receive Overrides
        
        // Step 3: Read previous iteration version files
        var previousFileVersionMatches = fileVersionResults
            .Where(d => passResults.Contains(d.PermitNumber.Trim()))
            .ToList();
        
        var results = new List<FileTemplateFinderResult>();
        
        results.AddRange(prevMatchResults.Select(p => new FileTemplateFinderResult
        {
            PermitNumber = p.PermitNumber,
            FileUrl = p.FileUrl,
            DateOfIssue = p.DateOfIssue,
            SignatureDate = p.SignatureDate,
            NaldIssueNumber = p.NaldIssueNo.ToString(),
            NaldIncrementNumber = p.NaldIncrementNo,
            FileName = LicenceFileHelpers.ExtractFilenameFromUrl(p.FileUrl),
        }));
        
        results.AddRange(previousFileVersionMatches.Select(p => new FileTemplateFinderResult
        {
            PermitNumber = p.PermitNumber,
            FileUrl = p.FileUrl,
            DateOfIssue = p.DateOfIssueOfEvaluatedFile,
            SignatureDate = p.SignatureDateOfFileEvaluated,
            NaldIssueNumber = p.NaldIssueNo.ToString(),
            NaldIncrementNumber = p.NaldIncrementNo,
            FileName = LicenceFileHelpers.ExtractFilenameFromUrl(p.FileUrl),
        }));
        
        results.AddRange(overrides.Select(p => new FileTemplateFinderResult
        {
            PermitNumber = p.PermitNumber,
            FileUrl = p.FileUrl,
            NaldIssueNumber = p.IssueNo,
            NaldIncrementNumber = p.IncrementNo,
            FileName = LicenceFileHelpers.ExtractFilenameFromUrl(p.FileUrl),
        }));
        
        // Generate output Excel file with appropriate header mapping for TemplateFinderResult
        var templateHeaderMapping = new Dictionary<string, string>
        {
            { "PermitNumber", "Permit Number" },
            { "FileUrl", "File URL" },
            { "DateOfIssue", "Date of Issue" },
            { "SignatureDate", "Signature Date" },
            { "NaldIssueNumber", "NALD Issue Number" },
            { "FileName", "File Name" }
        };

        var outputFileName = $"TemplateIdentificationResults_{DateTime.Now:yyyyMMdd_HHmmss}";
        var worksheetData = new List<(string SheetName, Dictionary<string, string>? HeaderMapping, object Data)>
        {
            ("Template Results", templateHeaderMapping, results
                .DistinctBy(f => f.FileUrl)
                .OrderBy(p => p.PermitNumber)
                .ToList())
        };

        return _fileProcessor.GenerateExcel(worksheetData, outputFileName);

    }

    /// <summary>
    /// Processes duplicate detection by identifying files that satisfy Priority4 rule and their potential duplicates
    /// </summary>
    /// <param name="dmsRecords">DMS extract records to process</param>
    /// <param name="naldRecords"></param>
    /// <returns>List of duplicate detection results</returns>
    private List<DuplicateResult> ProcessDuplicateDetection(
        List<DmsExtract> dmsRecords,
        List<NaldSimpleRecord> naldRecords)
    {
        var results = new List<DuplicateResult>();

        // Read NALD records to get region information
        var regionLookup = naldRecords.ToDictionary(
            n => n.DmsPermitNo,
            n => n.Region,
            StringComparer.OrdinalIgnoreCase);

        // Group records by permit number
        var groupedByPermit = dmsRecords
            .Where(r => !string.IsNullOrWhiteSpace(r.PermitNumber))
            .GroupBy(r => r.PermitNumber)
            .ToList();

        Console.WriteLine($"Processing {groupedByPermit.Count} permit groups for duplicate detection...");

        foreach (var permitGroup in groupedByPermit)
        {
            var permitNumber = permitGroup.Key;
            var filesInPermit = permitGroup.ToList();

            // Find files that satisfy Priority4 rule
            var priority4Files = filesInPermit
                .Where(dms => RuleHelpers.ContainsLicenseVariationPriority4(dms.FileName, dms.PermitNumber))
                .ToList();

            foreach (var priority4File in priority4Files)
            {
                // Look for potential duplicates (same name but missing first character)
                var expectedDuplicateName = priority4File.FileName.Length > 1 ? 
                    priority4File.FileName.Substring(1) : 
                    string.Empty;

                if (string.IsNullOrEmpty(expectedDuplicateName)) 
                    continue;

                // Find files with matching duplicate pattern
                var duplicateFiles = filesInPermit
                    .Where(dms => dms.FileName.Equals(expectedDuplicateName, StringComparison.OrdinalIgnoreCase) &&
                                 !dms.FileUrl.Equals(priority4File.FileUrl, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (!duplicateFiles.Any())
                {
                    continue;
                }
                
                foreach (var duplicateFile in duplicateFiles)
                {
                    // Get region information for this permit number
                    var region = regionLookup.TryGetValue(permitNumber, out var foundRegion) ? foundRegion : string.Empty;

                    // Add the Priority4 file as one row
                    results.Add(new DuplicateResult
                    {
                        PermitNumber = permitNumber,
                        FileUrl = priority4File.FileUrl,
                        FileName = priority4File.FileName,
                        Region = region
                    });

                    // Add the duplicate file as another row
                    results.Add(new DuplicateResult
                    {
                        PermitNumber = permitNumber,
                        FileUrl = duplicateFile.FileUrl,
                        FileName = duplicateFile.FileName,
                        Region = region
                    });
                }
            }
        }

        Console.WriteLine($"Duplicate detection completed. Found {results.Count} potential duplicates.");
        return results;
    }

    #region Private Helper Methods

    /// <summary>
    /// Processes license matching using configured rules
    /// </summary>
    /// <param name="dmsRecords">DMS extract records to search in</param>
    /// <param name="dmsManualFixes"></param>
    /// <param name="dmsChangeAuditOverrides"></param>
    /// <param name="dmsFileIdInformation"></param>
    /// <param name="generalApiClient"></param>
    /// <param name="naldRecordsToProcess">NALD extract records to process</param>
    /// <param name="naldData"></param>
    /// <param name="wradiToolScrapeResults"></param>
    /// <param name="licenceFinderPreviousIterationMatches"></param>
    /// <param name="wradiLocalFilesInventory"></param>
    /// <returns>List of license matching results</returns>
    private async Task<(List<LicenceMatchResult> LicenceMatchResults,
        List<UnmatchedLicenceMatchResult> UnmatchedLicenseMatchResults,
        List<DeltaResult> DeltaResults)>
        ProcessLicenceMatchingAsync(
            Dictionary<string, List<DmsExtract>> dmsRecords,
            Dictionary<string, DmsManualFixExtract> dmsManualFixes,
            List<Override> dmsChangeAuditOverrides,
            ConcurrentDictionary<Guid, List<DmsFileIdInformation>> dmsFileIdInformation,
            IGeneralApiClient generalApiClient,
            List<NaldSimpleRecord> naldRecordsToProcess,
            Dictionary<string, List<NaldLicenceVersion>> naldData,
            List<DmsFileReaderResult> wradiToolScrapeResults,
            List<LicenceMatchResult> licenceFinderPreviousIterationMatches,
            Dictionary<string, FileInventory> wradiLocalFilesInventory)
    {
        var unmatchedVersionResults = await FindUnmatchedLicenceFilesAsync(
            dmsRecords,
            naldData,
            licenceFinderPreviousIterationMatches,
            wradiToolScrapeResults,
            dmsFileIdInformation,
            generalApiClient);
        
        var deltaResults = new List<DeltaResult>();
        
        var dmsDictionaries = new DmsLookupIndexes
        {
            ByPermitNumber = dmsRecords,
            ByManualFixPermitNumber = BuildDmsManualFixDictionary(dmsRecords, dmsManualFixes)
        };
        
        const string scrapeNotAttemptedError = "Scrape not attempted";
        var processedRecordCount = 0;
        var rowIndex = 1;
        
        Console.WriteLine($"Processing {naldRecordsToProcess.Count} NALD records...");
        var returnList = new List<LicenceMatchResult>();
        
        // Process each record sequentially
        foreach (var naldReportRecord in naldRecordsToProcess)
        {
            var licenceMatchResult = new LicenceMatchResult
            {
                LicenseNumber = naldReportRecord.LicNo,
                PermitNumber = LicenceFileHelpers.CleanPermitNumber(naldReportRecord.LicNo),
                UniqueColumnID = rowIndex++.ToString()
            };
            
            var lowercasePermitNumber = licenceMatchResult.PermitNumber.ToLowerInvariant();
            
            var naldLicenceVersionData = naldData.TryGetValue(
                licenceMatchResult.PermitNumber,
                out var tempNaldDataRows)
                ? tempNaldDataRows
                    .OrderByDescending(r => r.IssueNo)
                    .ThenByDescending(r => r.IncrementNo)
                    .FirstOrDefault()
                : null;

            licenceMatchResult.IsWaterCompany = naldLicenceVersionData != null
                ? naldLicenceVersionData.ArepEiucCode?.EndsWith("SWC",
                    StringComparison.InvariantCultureIgnoreCase) == true
                : null;
            
            // Check if permit number exists in overrides first (IMPORTANT - there should only
            // be one override per permit number or we will intentionally error)
            var overrideRecords = dmsChangeAuditOverrides
                .Where(ca =>
                    ca.PermitNumber.Equals(licenceMatchResult.PermitNumber, StringComparison.OrdinalIgnoreCase)
                    || ca.LicenceReference.Equals(licenceMatchResult.LicenseNumber, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            if (overrideRecords.Count > 1)
            {
                throw new Exception(
                    $"Duplicate overrides found for {overrideRecords[0].PermitNumber} or {overrideRecords[0].LicenceReference}");
            }
            
            var overrideRecord = overrideRecords.FirstOrDefault();
            
            var naldVersionIssueNo = int.Parse(naldLicenceVersionData?.IssueNo ?? "0");
            var overrideIssueNo = !string.IsNullOrWhiteSpace(overrideRecord?.IssueNo)
                ? int.Parse(overrideRecord.IssueNo)
                : 0;
            
            var naldVersionIncrementNo = naldLicenceVersionData?.IncrementNo ?? 0;
            var overrideIncrementNo = overrideRecord?.IncrementNo ?? 0;
            
            licenceMatchResult.SignatureDate = naldLicenceVersionData?.SignatureDate?.ToString("dd/MM/yyyy");
            licenceMatchResult.NaldId = int.Parse(naldLicenceVersionData?.AablId ?? "0");
            licenceMatchResult.NaldIssueNo = int.Parse(naldLicenceVersionData?.IssueNo ?? "0");
            licenceMatchResult.NaldIncrementNo = naldLicenceVersionData?.IncrementNo;
            
            if (overrideRecord != null
                && overrideIssueNo >= naldVersionIssueNo
                && overrideIncrementNo >= naldVersionIncrementNo)
            {
                licenceMatchResult.SeenInDmsExtract = dmsRecords.ContainsKey(lowercasePermitNumber)
                    && dmsRecords[lowercasePermitNumber].Any(x => x.FileId == overrideRecord.FileId);
                
                var weHaveDownloaded = false;

                var (overridePermitNumberFormats, _) = GetPermitNumberFormats(
                    lowercasePermitNumber,
                    naldReportRecord.Region);
                
                foreach (var (permitNumber, _) in overridePermitNumberFormats)
                {
                    weHaveDownloaded = wradiLocalFilesInventory.ContainsKey(
                        $"{permitNumber.ToLower()}_{overrideRecord.FileId}");

                    if (weHaveDownloaded)
                    {
                        break;
                    }
                }

                if (!weHaveDownloaded)
                {
                        
                }

                licenceMatchResult.WeHaveDownloaded = weHaveDownloaded;
                licenceMatchResult.ChangeAuditAction = "Override";
                licenceMatchResult.FileUrl = overrideRecord.FileUrl;
                licenceMatchResult.DmsPermitNumber = GetDmsPermitNumber(overrideRecord.FileUrl);
                
                licenceMatchResult.NaldIssueNo = string.IsNullOrWhiteSpace(overrideRecord.IssueNo)
                    ? 0
                    : int.Parse(overrideRecord.IssueNo);
                licenceMatchResult.NaldIncrementNo = overrideRecord.IncrementNo;
            
                licenceMatchResult.RuleUsed = "Override";
                licenceMatchResult.Region = naldReportRecord.Region;
               
                licenceMatchResult.LiveLicenceFound = IsLiveLicenceFound(licenceMatchResult);
                returnList.Add(licenceMatchResult);

                var overrideFileId = Guid.TryParse(overrideRecord.FileId, out var tempOverrideFileId)
                    ? tempOverrideFileId
                    : Guid.Empty;
                
                var overrideScrapeResults = wradiToolScrapeResults
                    .Where(t =>
                        t.PermitNumber?.Equals(licenceMatchResult.PermitNumber, StringComparison.OrdinalIgnoreCase) == true
                        && t.FileId == overrideFileId)
                    .ToList();
                
                var overrideScrapeResult = overrideScrapeResults.FirstOrDefault();

                SetScrapedFields(licenceMatchResult, overrideScrapeResult, scrapeNotAttemptedError);
                
                licenceMatchResult.FileId = overrideRecord.FileId;
                var fileIdInfo = await RecordFileIdAsync(
                    overrideRecord.FileId,
                    overrideRecord.FileUrl,
                    dmsFileIdInformation,
                    generalApiClient);
                    
                licenceMatchResult.FileIdStatus = fileIdInfo?.Status;
                licenceMatchResult.FileIdStatusChangeDate = fileIdInfo?.StatusDateUtc.ToString("dd/MM/yyyy");
                
                processedRecordCount++;
                continue;
            }

            // Override record exists but was cancelled
            if (overrideRecord != null)
            {
                var becauseOfIncrement = overrideIssueNo >= naldVersionIssueNo;
                var reason = becauseOfIncrement ? "Increment No increased" : "Issue No increased";
                
                licenceMatchResult.ChangeAuditAction = $"Override cancelled ({reason})";
            }

            var (permitNumberFormats, permitNumberToUseForDms) = GetPermitNumberFormats(
                licenceMatchResult.PermitNumber,
                naldReportRecord.Region);

            var folderFound = false;
            
            foreach (var (permitNumber, folderNameAutoCorrected) in permitNumberFormats)
            {
                licenceMatchResult.FolderNameAutoCorrect = folderNameAutoCorrected;
                var lowerCasePermitNumber = permitNumber.ToLowerInvariant();
                
                if (!dmsDictionaries.ByPermitNumber.ContainsKey(lowerCasePermitNumber)
                    && !dmsDictionaries.ByManualFixPermitNumber.ContainsKey(lowerCasePermitNumber))
                {
                    continue;
                }
                
                folderFound = true;
                permitNumberToUseForDms = permitNumber;
                
                break;
            }

            var ruleUsed = "No Match";

            if (!folderFound)
            {
                ruleUsed = "Not Applicable";
                licenceMatchResult.FileUrl = "No Folder Found";
            }
            else
            {
                DmsExtract? matchedDmsRecord = null;
                
                // Try each rule in priority order until a match is found
                foreach (var ruleToMatch in _rulesToMatch)
                {
                    matchedDmsRecord = ruleToMatch.FindMatch(
                        permitNumberToUseForDms,
                        dmsDictionaries);

                    if (matchedDmsRecord == null)
                    {
                        continue;
                    }
                    
                    ruleUsed = ruleToMatch.RuleName;
                    break;
                }

                // Populate result based on match outcome
                if (matchedDmsRecord != null)
                {
                    var weHaveDownloaded = false;

                    foreach (var (permitNumber, _) in permitNumberFormats)
                    {
                        weHaveDownloaded = wradiLocalFilesInventory.ContainsKey(
                            $"{permitNumber.ToLower()}_{matchedDmsRecord.FileId}");

                        if (weHaveDownloaded)
                        {
                            break;
                        }
                    }

                    if (!weHaveDownloaded)
                    {
                        
                    }
                    
                    licenceMatchResult.SeenInDmsExtract = true;
                    licenceMatchResult.WeHaveDownloaded = weHaveDownloaded;
                    licenceMatchResult.FileUrl = matchedDmsRecord.FileUrl;
                    licenceMatchResult.DmsPermitNumber = GetDmsPermitNumber(matchedDmsRecord.FileUrl);
                    licenceMatchResult.OtherReference = matchedDmsRecord.OtherReference;
                    licenceMatchResult.FileSize = matchedDmsRecord.FileSize;
                    licenceMatchResult.DisclosureStatus = matchedDmsRecord.DisclosureStatus;
                    licenceMatchResult.DocumentDate = matchedDmsRecord.DocumentDate;
                    licenceMatchResult.FileId = matchedDmsRecord.FileId;

                    if (licenceMatchResult.WeHaveDownloaded == false)
                    {
                        deltaResults.Add(new DeltaResult
                        {
                            PermitNumber = licenceMatchResult.PermitNumber,
                            FileUrl = licenceMatchResult.FileUrl
                        });
                    }
                    
                    var fileIdInfo = await RecordFileIdAsync(
                        matchedDmsRecord.FileId,
                        matchedDmsRecord.FileUrl,
                        dmsFileIdInformation,
                        generalApiClient);
                    
                    licenceMatchResult.FileIdStatus = fileIdInfo?.Status;
                    licenceMatchResult.FileIdStatusChangeDate = fileIdInfo?.StatusDateUtc.ToString("dd/MM/yyyy");
                }
                else
                {
                    licenceMatchResult.FileUrl = "No Match Found";
                }
            }

            var fileId = !string.IsNullOrEmpty(licenceMatchResult.FileId)
                ? Guid.TryParse(licenceMatchResult.FileId, out var tempFileId) ? tempFileId : null
                : (Guid?)null;

            if (fileId == null)
            {
                var count = wradiToolScrapeResults
                    .Count(sr => sr.PermitNumber == permitNumberToUseForDms);
                
                licenceMatchResult.FileId = $"({count} files scraped)";
            }
            
            var fileIdError = fileId == null;
            const string fileIdNotSetError = "File id not set";

            var fileIdOrScrapeError = fileIdError ? fileIdNotSetError : scrapeNotAttemptedError;
            
            var scrapeResult = wradiToolScrapeResults.FirstOrDefault(
                r => fileId != null && r.FileId == fileId);
            
            SetScrapedFields(licenceMatchResult, scrapeResult, fileIdOrScrapeError);
            
            licenceMatchResult.RuleUsed = ruleUsed;
            licenceMatchResult.Region = naldReportRecord.Region;
            licenceMatchResult.DoiSignatureDateMatch = licenceMatchResult.SignatureDate == licenceMatchResult.DateOfIssue;

            var versionMatch = unmatchedVersionResults.FirstOrDefault(uvr =>
                uvr.PermitNumber.Equals(permitNumberToUseForDms, StringComparison.OrdinalIgnoreCase));
            
            licenceMatchResult.IncludedInVersionMatch = versionMatch != null;
            licenceMatchResult.SingleLicenceInVersionMatch = versionMatch?.FileDeterminedAsLicence;
            licenceMatchResult.VersionMatchFileUrl = versionMatch?.FileUrl;
            licenceMatchResult.DuplicateLicenceInVersionMatchResult = versionMatch?.LicenceCount > 1;
            licenceMatchResult.NaldIssue = versionMatch?.NaldDataQualityIssue;

            licenceMatchResult.LiveLicenceFound = IsLiveLicenceFound(licenceMatchResult);
            returnList.Add(licenceMatchResult);
            
            processedRecordCount++;

            Console.WriteLine($"Processing record {processedRecordCount}/{naldRecordsToProcess.Count}: {naldReportRecord.LicNo} - {ruleUsed}");
        }

        Console.WriteLine($"Licence matching completed. Total records processed: {processedRecordCount}");
        
        return (returnList, unmatchedVersionResults, deltaResults);
    }

    private static (List<(string PermitNumber, bool FolderNameAutoCorrect)> List, string permitNumberToUseForDms) GetPermitNumberFormats(
        string permitNumber,
        string regionName)
    {
        var permitNumberFormats = new List<(string PermitNumber, bool FolderNameAutoCorrect)>
        {
            (permitNumber, false)
        };

        var permitNumberToUseForDms = permitNumberFormats[0].PermitNumber;
            
        var isMidlands = regionName == "Midlands";
        var shouldDropTheInitialZero = permitNumberToUseForDms.StartsWith("032");

        if (isMidlands && shouldDropTheInitialZero)
        {
            permitNumberFormats.Add((permitNumberToUseForDms[1..], true));
        }
        
        return (permitNumberFormats, permitNumberToUseForDms);
    }
    
    private static void SetScrapedFields(
        LicenceMatchResult licenceMatchResult,
        DmsFileReaderResult? scrapeResult,
        string fileIdOrScrapeError)
    {
        var dateOfIssue = scrapeResult != null
            ? LicenceFileHelpers.ConvertDateToStandardFormat(scrapeResult.DateOfIssue.ToString())
            : fileIdOrScrapeError;

        licenceMatchResult.DateOfIssue = dateOfIssue;
        licenceMatchResult.PrimaryTemplate = scrapeResult != null
            ? scrapeResult.PrimaryType //PrimaryTemplateType
            : fileIdOrScrapeError;
            
        licenceMatchResult.SecondaryTemplate = scrapeResult != null
            ? scrapeResult.SecondaryType //SecondaryTemplateType
            : fileIdOrScrapeError;
        
        licenceMatchResult.NumberOfPages = scrapeResult != null
            ? scrapeResult.NumberOfPages
            : -1;
    }
    
    private static bool IsLiveLicenceFound(LicenceMatchResult licenceMatchResult)
    {
        return licenceMatchResult.DoiSignatureDateMatch
               || licenceMatchResult.ChangeAuditAction == "Override";
    }
    
    private static string GetDmsPermitNumber(string? fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
        {
            return string.Empty;
        }
		
        var parts = fileUrl.Split('/');
		
        if (parts.Length < 9)
        {
            return string.Empty;
        }
		
        var permitNumberPart = parts[6];		
        return permitNumberPart;
    }

    private static async Task<DmsFileIdInformation?> RecordFileIdAsync(
        string? fileId,
        string? fileUrl,
        ConcurrentDictionary<Guid, List<DmsFileIdInformation>> dmsFileIdInformation,
        IGeneralApiClient generalApiClient)
    {
        if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(fileUrl))
        {
            return null;
        }

        if (!Guid.TryParse(fileId, out var fileIdGuid))
        {
            // File id is not a guid
            return new DmsFileIdInformation
            {
                Status = "ERROR - File id is not a valid guid",
                StatusDateUtc = DateTime.UtcNow
            };
        }
        
        var beforeRecordList = dmsFileIdInformation.GetValueOrDefault(fileIdGuid);

        var outputDmsFileIdInformation = new DmsFileIdInformation
        {
            FileId = fileIdGuid,
            DmsFilePath = fileUrl,
            ProcessRunId = -1,
            StatusDateUtc = DateTime.UtcNow
        };

        if (beforeRecordList == null)
        {
            outputDmsFileIdInformation.Status = "FirstSeen";
            
            await generalApiClient.AddDmsFileIdInformationAsync(outputDmsFileIdInformation);
            dmsFileIdInformation.TryAdd(outputDmsFileIdInformation.FileId, [outputDmsFileIdInformation]);
        }
        else
        {
            var lastRecord = beforeRecordList
                .OrderByDescending(r => r.StatusDateUtc)
                .First();

            var noChange = lastRecord.DmsFilePath == fileUrl;

            if (noChange)
            {
                return lastRecord;
            }
            
            var lastRecordFilenameOnly = lastRecord.DmsFilePath![(lastRecord.DmsFilePath!.LastIndexOf('/') + 1)..];
            var filenameOnly = fileUrl[(fileUrl.LastIndexOf('/') + 1)..];
            
            var isFilenameSame = lastRecordFilenameOnly == filenameOnly;
            outputDmsFileIdInformation.Status = isFilenameSame ? "Moved" : "Renamed";

            await generalApiClient.AddDmsFileIdInformationAsync(outputDmsFileIdInformation);
            dmsFileIdInformation[outputDmsFileIdInformation.FileId].Add(outputDmsFileIdInformation);
        }

        return outputDmsFileIdInformation;
    }
    
    /// <summary>
    /// Checks for NALD data issues and creates appropriate result record if issues are found
    /// </summary>
    /// <param name="matchedNaldRecord">The matched NALD record</param>
    /// <param name="naldRecordsForPermit">All NALD records for the permit</param>
    /// <param name="record">The license match record</param>
    /// <param name="dmsRecordsForPermit">DMS records for the permit</param>
    /// <param name="fileIdentification">File identification record</param>
    /// <param name="allMatchingIdentificationFiles"></param>
    /// <returns>UnmatchedLicenseMatchResult if NALD data issue found, null otherwise</returns>
    private UnmatchedLicenceMatchResult? CheckForNaldDataIssue(
        NaldLicenceVersion? matchedNaldRecord,
        List<NaldLicenceVersion> naldRecordsForPermit,
        LicenceMatchResult record,
        List<DmsExtract> dmsRecordsForPermit,
        DmsFileReaderResult fileIdentification,
        List<DmsFileReaderResult> allMatchingIdentificationFiles)
    {
        // Can't find a NALD record
        if (matchedNaldRecord == null)
        {
            return new UnmatchedLicenceMatchResult
            {
                PermitNumber = record.PermitNumber,
                FileUrl = dmsRecordsForPermit.FirstOrDefault(d => d.FileName.Equals(fileIdentification.FileName, StringComparison.OrdinalIgnoreCase))?.FileUrl ?? string.Empty,
                SignatureDateOfFileEvaluated = matchedNaldRecord?.SignatureDate?.ToString("dd/MM/yyyy"),
                LicenseNumber = record.LicenseNumber,
                Region = record.Region,
                FileEvaluated = fileIdentification.FileName,
                FileTypeEvaluated = fileIdentification.FileType,
                FileDeterminedAsLicence = false,
                DateOfIssueOfEvaluatedFile = fileIdentification.DateOfIssue?.ToString("dd/MM/yyyy"),
                NaldDataQualityIssue = true,
                OriginalFileUrlIdentifiedAsLicence = record.FileUrl
            };
        }

        // We didn't find an issue number in the NALD record?? TODO
        if (!int.TryParse(matchedNaldRecord.IssueNo, out var matchedIssueNo))
        {
            return null;
        }
        
        var higherIssueRecords = naldRecordsForPermit
            .Where(n => int.TryParse(n.IssueNo, out var issueNo) 
                && issueNo >= matchedIssueNo 
                && n.SignatureDate == null)
            .ToList();

        var doiMatchingSignatureDate = higherIssueRecords.All(h =>
            allMatchingIdentificationFiles.Any(a => a.DateOfIssue == h.SignatureDate));
        
        // There are no higher issue records fiybd, or there are and they don't match the signature date
        if (!higherIssueRecords.Any() || (higherIssueRecords.Any() && !doiMatchingSignatureDate))
        {
            return new UnmatchedLicenceMatchResult
            {
                PermitNumber = record.PermitNumber,
                FileUrl = dmsRecordsForPermit.FirstOrDefault(d => d.FileName.Equals(fileIdentification.FileName, StringComparison.OrdinalIgnoreCase))?.FileUrl ?? string.Empty,
                SignatureDateOfFileEvaluated = matchedNaldRecord.SignatureDate?.ToString("dd/MM/yyyy"),
                LicenseNumber = record.LicenseNumber,
                Region = record.Region,
                FileEvaluated = fileIdentification.FileName,
                FileTypeEvaluated = fileIdentification.FileType,
                FileDeterminedAsLicence = false,
                DateOfIssueOfEvaluatedFile = fileIdentification.DateOfIssue?.ToString("dd/MM/yyyy"),
                NaldDataQualityIssue = true,
                OriginalFileUrlIdentifiedAsLicence = record.FileUrl,
                NaldId = int.Parse(matchedNaldRecord.AablId ?? "0"),
                NaldIssueNo = int.Parse(matchedNaldRecord.IssueNo),
                NaldIncrementNo = matchedNaldRecord.IncrementNo
            };
        }

        return null;
    }

    /// <summary>
    /// Builds manual index from DMS records for optimized searching
    /// </summary>
    /// <param name="dmsRecords">The DMS records to build indexes from</param>
    /// <param name="dmsManualFixes"></param>
    /// <returns>DMSLookupIndexes containing various lookup dictionaries</returns>
    private static Dictionary<string, List<DmsExtract>> BuildDmsManualFixDictionary(
        Dictionary<string, List<DmsExtract>> dmsRecords,
        Dictionary<string, DmsManualFixExtract> dmsManualFixes)
    {
        var returnDict = new Dictionary<string, List<DmsExtract>>();

        foreach (var dmsRecord in dmsRecords)
        {
            var permitNumber = dmsRecord.Key.Trim();
            
            // Index by permit number (exact)
            if (string.IsNullOrWhiteSpace(permitNumber))
            {
                continue;
            }

            if (!dmsManualFixes.ContainsKey(permitNumber))
            {
                continue;
            }
            
            if (!returnDict.ContainsKey(permitNumber))
            {
                returnDict.Add(permitNumber, []);
            }

            returnDict[permitNumber].AddRange(dmsRecord.Value);
        }

        return returnDict;
    }

    #endregion
}
