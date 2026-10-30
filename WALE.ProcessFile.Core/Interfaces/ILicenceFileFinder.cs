using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.Nald;

namespace WALE.ProcessFile.Core.Interfaces;

/// <summary>
/// Interface for finding and matching license files
/// </summary>
public interface ILicenceFileFinder
{
    /// <summary>
    /// Finds and matches license files, generating an Excel report with results
    /// </summary>
    /// <returns>The path to the generated Excel results file</returns>
    Task<string> FindLicenceFilesAsync(
        Dictionary<string, List<DmsExtract>> dmsRecords,
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
        string dmsDate);
    
    /// <summary>
    /// Finds all potential duplicates in DMS extract
    /// </summary>
    /// <returns>The path to the generated Excel results file</returns>
    string FindDuplicateLicenceFiles(List<DmsExtract> dmsRecords, List<NaldSimpleRecord> naldRecords);

    string BuildFileTemplateIdentificationExtract(
        List<LicenceMatchResult> previousIterationMatches,
        List<Override> overrides,
        List<UnmatchedLicenceMatchResult> fileVersionResults);

    string FindLicenceFilesToDownload_SpreadsheetCompareOnly(
        List<DmsExtract> dmsRecords,
        List<LicenceMatchResult> previousIterationMatches,
        List<LicenceMatchResult> currentIterationMatches,
        string? region = null);

    Task<string> FindAllFilesToDownloadAsync(
        Dictionary<string, List<DmsExtract>> dmsRecords,
        List<LicenceMatchResult> currentIterationMatches,
        Dictionary<string, FileInventory> wradiAllLocalFilesInventory,
        IGeneralApiClient apiClient);
    
    string FindLicenceFilesToDownload(
        List<DmsExtract> dmsRecords,
        List<LicenceMatchResult> currentIterationMatches,
        Dictionary<string, FileInventory> wradiAllLocalFilesInventory,
        string? filterRegion = null);

    /// <summary>
    /// Filters the DMS extract for inspection report (WR51) PDFs, using filename/folder rules
    /// Saves results to inspection_report_finder_result via the API (clear then chunked save)
    /// then also writes an Excel report for human review, including a "Files Needed Locally
    /// (Delta)" tab of matched files not yet in wradiLocalFilesInventory.
    /// </summary>
    /// <returns>The path to the generated Excel results file</returns>
    Task<string> FindInspectionReportFilesAsync(
        List<DmsExtract> dmsRecords,
        IGeneralApiClient generalApiClient,
        Dictionary<string, FileInventory> wradiLocalFilesInventory);
}