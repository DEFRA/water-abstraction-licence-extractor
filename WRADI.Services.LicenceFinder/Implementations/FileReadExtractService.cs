using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.Nald;
using WRADI.Services.LicenceFinder.Helpers;

namespace WRADI.Services.LicenceFinder.Implementations;

/// <summary>
/// Service for reading and extracting data from files
/// </summary>
public class FileReadExtractService(ILicenceFileProcessor fileProcessor) : IReadExtract
{
    private readonly ILicenceFileProcessor _fileProcessor = fileProcessor
        ?? throw new ArgumentNullException(nameof(fileProcessor));

    /// <summary>
    /// Common header mapping for LicenseMatchResult - maps property names to Excel header names
    /// </summary>
    private static readonly Dictionary<string, List<string>> LicenseMatchResultHeaderMapping = new()
    {
        { "PermitNumber", ["Permit Number" ]},
        { "FileUrl", ["File URL"]},
        { "RuleUsed", ["Rule Used" ]},
        { "LicenseNumber", ["License Number" ]},
        { "DocumentDate", ["Document Date" ]},
        { "SignatureDate", ["Latest issued signature date" ]},
        { "NaldID", ["NALD AABL_ID" ]},
        { "NaldIssueNo", ["NALD Issue No.", "NALD Issue_No" ]},
        { "NaldIncrementNo", ["NALD Increment No.", "NALD Increment_No" ]},
        { "DateOfIssue", ["Scrapped Date of Issue" ]},
        { "MatchFound", ["Match Found" ]},
        { "FoundMultipleMatches", ["Found Multiple Matches" ]},
        { "MatchDetails", ["Match Details" ]},
        { "OtherReference", ["Other Reference" ]},
        { "FileSize", ["File Size" ]},
        { "DisclosureStatus", ["Disclosure Status" ]},
        { "Region", ["Region" ]},
        { "PreviousIterationRuleUsed", ["Previous Iteration Rule Used" ]},
        { "DifferenceInRuleusedInIterations", ["Difference In Rule Used In Iterations" ]},
        { "PreviousIterationFileUrl", ["Previous Iteration File URL" ]},
        { "DifferenceInFileUrlInIterations", ["Difference In File URL In Iterations" ]},
        { "FileId", ["File ID" ]},
        { "DOISignatureDateMatch", ["Latest issued signature date = Scraped Date of Issue" ]},
        { "ChangeAuditAction", ["Override Action" ]},
        { "IncludedInVersionMatch", ["Included in VersionMatch process" ]},
        { "SingleLicenceInVersionMatch", ["Single Licence found in VersionMatch process" ]},
        { "VersionMatchFileUrl", ["Version Match Licence URL" ]},
        { "DuplicateLicenceInVersionMatchResult", ["Duplicate licences found in VersionMatch process" ]}
    };

    /// <summary>
    /// Reads all files starting with 'Consolidated' from the resources folder
    /// </summary>
    /// <returns>Combined list of DMS extract records from all matching files</returns>
    public Dictionary<string, List<DmsExtract>> GetDmsExtracts()
    {
        var allDmsRecords = new Dictionary<string, List<DmsExtract>>(StringComparer.OrdinalIgnoreCase);
        var filenames = _fileProcessor.FindFilesByPattern("Consolidated");

        foreach (var filename in filenames)
        {
            var records = _fileProcessor.ExtractExcel<List<DmsExtract>>(
                filename,
                new Dictionary<string, List<string>>
                {
                    {"Site Collection", ["SiteCollection"]},
                    {"Permit Number", ["PermitNumber"]},
                    {"Document Date", ["DocumentDate"]},
                    {"Uploaded Date", ["UploadDate"]},
                    {"File URL", ["FileUrl"]},
                    {"File Name", ["FileName"]},
                    {"File Size", ["FileSize"]},
                    {"Disclosure Status", ["DisclosureStatus"]},
                    {"Other Reference", ["OtherReference"]},
                    {"Modified Date", ["ModifiedDate"]},
                    {"File ID", ["FileId"]}
                },
                [
                    "ActivityGrouping",
                    "EPRNumber",
                    "CurrentPermit"
                ]);

            foreach (var record in records)
            {
                var completed = AddToListFoldersWithWordAndIn(record, allDmsRecords);
                if (completed)
                {
                    continue;
                }
                
                if (allDmsRecords.TryGetValue(record.PermitNumber, out var list2))
                {
                    list2.Add(record);
                    continue;
                }
                
                allDmsRecords.Add(record.PermitNumber, [record]);
            }
        }

        return allDmsRecords;
    }

    private static bool AddToListFoldersWithWordAndIn(
        DmsExtract dmsRecord,
        Dictionary<string, List<DmsExtract>> allDmsRecords)
    {
        const string andString = "AND";
        var dontWorkAroundCombinedDmsFolders = true;

        if (dontWorkAroundCombinedDmsFolders)
        {
            if (dmsRecord.PermitNumber.Contains(andString, StringComparison.InvariantCultureIgnoreCase))
            {
                // Don't add anything
                return true;
            }

            return false;
        }

        const string sString = "S";
        const string gString = "G";
        const string iString = "I";
        const string aAndBString = "AANDB";
        
        dmsRecord.PermitNumber = dmsRecord.PermitNumber
            .Replace("sandi", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("sandg", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("iands", sString, StringComparison.InvariantCultureIgnoreCase)
            .Replace("iandg", sString, StringComparison.InvariantCultureIgnoreCase);

        if (dmsRecord.PermitNumber.Contains(aAndBString, StringComparison.InvariantCultureIgnoreCase))
        {
            var parts = dmsRecord.PermitNumber.ToUpper().Split(andString);
            dmsRecord.PermitNumber = parts[0];
            
            if (allDmsRecords.TryGetValue(dmsRecord.PermitNumber, out var listA))
            {
                listA.Add(dmsRecord);
            }
            else
            {
                allDmsRecords.Add(dmsRecord.PermitNumber, [dmsRecord]);
            }

            var clonedDmsRecord = dmsRecord.Clone();
            clonedDmsRecord.PermitNumber = clonedDmsRecord.PermitNumber[..^1] + 'B';
            
            if (allDmsRecords.TryGetValue(clonedDmsRecord.PermitNumber, out var listB))
            {
                listB.Add(clonedDmsRecord);
            }
            else
            {
                allDmsRecords.Add(clonedDmsRecord.PermitNumber, [clonedDmsRecord]);
            }
            
            return true;
        }
        
        if (dmsRecord.PermitNumber.Contains(andString, StringComparison.InvariantCultureIgnoreCase))
        {
            var parts = dmsRecord.PermitNumber.ToUpper().Split(andString);
            var firstPart  = parts[0];
            char splitChar;
            
            if (firstPart.Contains(gString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = gString[0];
            }
            else if (firstPart.Contains(iString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = iString[0];
            }
            else if (firstPart.Contains(sString, StringComparison.InvariantCultureIgnoreCase))
            {
                splitChar = sString[0];
            }
            else
            {
                throw new NotImplementedException();
            }
            
            var beforeAndAfterLicenceChar = firstPart.Split(splitChar);
            var sharedPart = beforeAndAfterLicenceChar[0] + splitChar;

            var suffix1 = beforeAndAfterLicenceChar[1];
            dmsRecord.PermitNumber = $"{sharedPart}{suffix1}";
            
            if (allDmsRecords.TryGetValue(dmsRecord.PermitNumber, out var list0))
            {
                list0.Add(dmsRecord);
            }
            else
            {
                allDmsRecords.Add(dmsRecord.PermitNumber, [dmsRecord]);
            }

            foreach (var suffix in parts.Skip(1))
            {
                var clonedDmsRecord = dmsRecord.Clone();
                clonedDmsRecord.PermitNumber = $"{sharedPart}{suffix}";
                
                if (allDmsRecords.TryGetValue(clonedDmsRecord.PermitNumber, out var list1))
                {
                    list1.Add(clonedDmsRecord);
                    continue;
                }
        
                allDmsRecords.Add(clonedDmsRecord.PermitNumber, [clonedDmsRecord]);
            }
            
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Reads all files starting with 'NALD_Extract' from the resources folder
    /// </summary>
    /// <returns>Combined list of NALD extract records from all matching files</returns>
    public List<NaldSimpleRecord> GetNaldReportRecords()
    {
        var allNaldRecords = new List<NaldSimpleRecord>();
        var naldFiles = _fileProcessor.FindFilesByPattern("NALD_Extract");

        foreach (var fileName in naldFiles)
        {
            var records = _fileProcessor.ExtractExcel<List<NaldSimpleRecord>>(
                fileName,
                new Dictionary<string, List<string>>
                {
                    { "Licence No.", ["LicNo"]},
                    { "Region", ["Region"]}
                },
                [
                    "WALicTypeDescription",
                    "OrigEffectiveDate",
                    "ExpiryDate",
                    "VersionStartDate",
                    "MaxAnnualQuantity",
                    "MaxDailyQuantity",
                    "PurposePointDescriptor",
                    "AggregatetoOtherLic",
                    "Salutation",
                    "Initials",
                    "Forename",
                    "Name",
                    "Line1",
                    "Line2",
                    "Line3",
                    "Line4",
                    "Town",
                    "County",
                    "Postcode",
                    "SourceType",
                    "PermitNo" // In destination model - not in Excel
                ]);

            // Enrich records with cleaned permit numbers
            foreach (var record in records)
            {
                record.DmsPermitNo = CleanPermitNumber(record.LicNo);
            }

            allNaldRecords.AddRange(records);
        }

        return allNaldRecords;
    }

    /// <summary>
    /// Reads Previous Iteration Matches from the resources folder
    /// </summary>
    /// <returns>Previous iteration match results</returns>
    public List<LicenceMatchResult> GetLicenceFinderPreviousIterationResults(string filename, string? region)
    {
        var allPreviousIterationResults = new List<LicenceMatchResult>();

        if (string.IsNullOrWhiteSpace(filename))
        {
            return allPreviousIterationResults;
        }
        
        var prevIterationMatch =
            _fileProcessor.FindFilesByPattern(filename).FirstOrDefault();

        if (prevIterationMatch == null)
        {
            throw new FileNotFoundException($"No files were found with the given filename '{filename}'.");
        }
        
        var records = _fileProcessor.ExtractExcel<List<LicenceMatchResult>>(
            prevIterationMatch,
            ReverseMapping(LicenseMatchResultHeaderMapping),
            [
                "NALDAABL_ID",
                "NALDIssue_No",
                "SignaturedateDQissuefoundinVersionMatchprocess",
                "NaldIssue", // This isn't in the Excel - it gets set later on,
                "NaldIncrementNo", // This isn't in the Excel - it gets set later on
                "FileId", // This isn't in the Excel - it gets set later on
                "PreviousIterationRuleUsed",
                "DifferenceInRuleusedInIterations",
                "PreviousIterationFileUrl",
                "DifferenceInFileUrlInIterations",
                "FileIdStatus", // TODO remove this when we have a new file that contains it - 2025-03-19
                "FileIdStatusChangeDate", // TODO remove this when we have a new file that contains it - 2025-03-19
                "IsWaterCompany", // TODO remove this when we have a new file that contains it - 2025-03-20
                "FolderNameAutoCorrect", // TODO remove this when we have a new file that contains it - 2025-03-23
                "SeenInDmsExtract",
                "WeHaveDownloaded"
            ]);
        
        allPreviousIterationResults.AddRange(records);

        if (string.IsNullOrWhiteSpace(region))
        {
            return allPreviousIterationResults;
        }
        
        return allPreviousIterationResults
            .Where(r => r.Region == region)
            .ToList();
    }

    /// <summary>
    /// Reads Change_Audit.xlsx file from the resources folder
    /// </summary>
    /// <returns>List of change audit records</returns>
    public List<ChangeAudit> ReadChangeAuditFiles()
    {
        var allChangeAudits = new List<ChangeAudit>();
        var changeAuditFiles = _fileProcessor.FindFilesByPattern("Change_Audit");

        foreach (var fileName in changeAuditFiles)
        {
            var records = _fileProcessor.ExtractExcel<List<ChangeAudit>>(
                fileName,
                new Dictionary<string, List<string>>
            {
                {"Permit Number", ["PermitNumber"]},
                {"Original File Path", ["OriginalPath"]},
                {"New File Path", ["UpdatedPath"]},
                {"Action", ["Action"]}
            });

            allChangeAudits.AddRange(records);
        }

        return allChangeAudits;
    }

    /// <summary>
    /// Reads Change_Audit.xlsx file from the resources folder
    /// </summary>
    /// <returns>List of change audit records</returns>
    public (List<Override>, string) GetDmsChangeAuditOverrides(string filename)
    {
        var allOverrides = new List<Override>();
        var overridesFilenames = _fileProcessor.FindFilesByPattern(filename);

        if (!overridesFilenames.Any())
        {
            throw new FileNotFoundException($"No override files were found with the given filename '{filename}'.");
        }
        
        if (overridesFilenames.Count != 1)
        {
            throw new FileNotFoundException("Only 1 override file should be used.");
        }
        
        Console.WriteLine($"Override file is {overridesFilenames[0]}");
        
        foreach (var fileName in overridesFilenames)
        {
            var records = _fileProcessor.ExtractExcel<List<Override>>(
                fileName,
                new Dictionary<string, List<string>>
                {
                    { "Permit Number", ["PermitNumber"]},
                    { "LicenceReference", ["LicenceReference"]},
                    { "File URL", ["FileUrl"]},
                    { "FullPath", ["FileUrl"]},
                    { "DefinitiveURL", ["FileUrl"]},
                    { "NALD Issue_No", ["IssueNo"]},
                    { "NALD Issue No.", ["IssueNo"]},
                    { "NALD Increment_No", ["IncrementNo"]},
                    { "NALD Increment No.", ["IncrementNo"]},
                    { "OverrideType", ["OverrideType"]},
                    { "File ID", ["FileId"]}
                },
                ["Duplicatecheck"]);

            allOverrides.AddRange(records);
        }

        return (allOverrides, overridesFilenames.First());
    }

    /// <summary>
    /// Reads all files starting with 'Manual_Fix_Extract' from the resources folder
    /// </summary>
    /// <returns>Combined list of manual fix extract records from all matching files</returns>
    public Dictionary<string, DmsManualFixExtract> GetDmsManualFixes()
    {
        var allManualFixes = new Dictionary<string, DmsManualFixExtract>(StringComparer.OrdinalIgnoreCase);
        var manualFixFiles = _fileProcessor.FindFilesByPattern("Manual_Fix_Extract");

        foreach (var fileName in manualFixFiles)
        {
            var records = _fileProcessor.ExtractExcel<List<DmsManualFixExtract>>(
                fileName,
                new Dictionary<string, List<string>>
                {
                    {"DMS Version Of Licence No.", ["PermitNumber"]},
                    {"DMS Permit Folder No.", ["PermitNumberFolder"]},
                },
                ["NALDLicenceNo"]);
            
            foreach (var record in records)
            {
                allManualFixes.TryAdd(record.PermitNumberFolder, record);
            }
        }

        return allManualFixes;
    }

    /// <summary>
    /// Reads LicenceVersionResult.xlsx file from the resources folder
    /// </summary>
    /// <returns>List of licence version records</returns>
    public List<UnmatchedLicenceMatchResult> ReadFileVersionResultsFile()
    {
        var allFileversionResults = new List<UnmatchedLicenceMatchResult>();
        var fileversionResults = _fileProcessor.FindFilesByPattern("LicenceVersionResults");

        foreach (var fileName in fileversionResults)
        {
            var records = _fileProcessor.ExtractExcel<List<UnmatchedLicenceMatchResult>>(
                fileName, 
                new Dictionary<string, List<string>>
                {
                    {"Permit Number", ["PermitNumber"]},
                    {"File URL", ["FileUrl"]},
                    {"Signature Date Of File Evaluated", ["SignatureDateOfFileEvaluated"]},
                    {"File Determined As Licence", ["FileDeterminedAsLicence"]},
                    {"Date of Issue Of Evaluated File", ["DateOfIssueOfEvaluatedFile"]},
                    {"NALD Issue No.", ["NALDIssueNo"]},
                    {"NALD Increment No.", ["NALDIncrementNo"]},
                    {"Is NALD Data Quality Issue", ["NALDDataQualityIssue"]}
                }, 
                [
                    "LicenceCount",
                    "FileId", // Not in the Excel file
                    "FileIdStatus", // TODO remove this when we have a new file that contains it - 2025-03-19
                    "FileIdStatusChangeDate", // TODO remove this when we have a new file that contains it - 2025-03-19
                    "IsWaterCompany", // TODO remove this when we have a new file that contains it - 2025-03-20
                    "NaldIncrementNo" // TODO remove this when we have a new file that contains it - 2025-03-20
                ]);

            // Update DateOfIssue format for all records
            foreach (var record in records)
            {
                record.DateOfIssueOfEvaluatedFile = LicenceFileHelpers.ConvertDateToStandardFormat(record.DateOfIssueOfEvaluatedFile);
            }

            allFileversionResults.AddRange(records);
        }
        
        return allFileversionResults;
    }
    
    #region Helper Methods

    /// <summary>
    /// Reverses a dictionary mapping for reading operations (Excel header to property name)
    /// </summary>
    /// <param name="mapping">The original mapping</param>
    /// <returns>Reversed mapping</returns>
    private static Dictionary<string, List<string>> ReverseMapping(Dictionary<string, List<string>> mapping)
    {
        var returnDict = new Dictionary<string, List<string>>();

        foreach (var line in mapping)
        {
            foreach (var lineValue in line.Value)
            {
                returnDict.Add(lineValue, [line.Key]);
            }
        }

        return returnDict;
    }

    /// <summary>
    /// Cleans a license number by removing forward slashes and asterisks
    /// </summary>
    /// <param name="licNo">The original license number (e.g., "6/33/03/*G/0038")</param>
    /// <returns>Cleaned permit number (e.g., "633303G0038")</returns>
    private static string CleanPermitNumber(string licNo)
    {
        if (string.IsNullOrWhiteSpace(licNo))
        {
            return string.Empty;
        }

        // Remove forward slashes and asterisks
        return licNo.Replace("/", "").Replace("*", "");
    }

    /// <summary>
    /// Removes permit number prefix from filename if it follows the pattern "permitnumber__name"
    /// </summary>
    /// <param name="filename">The filename that may contain a permit number prefix</param>
    /// <returns>Filename with permit number prefix removed (substring after first "__"), or original filename if no prefix found</returns>
    private static string? RemovePermitNumberPrefixFromFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return filename;

        // Find the first occurrence of "__"
        int doubleUnderscoreIndex = filename.IndexOf("__", StringComparison.Ordinal);

        if (doubleUnderscoreIndex >= 0)
        {
            // Return substring after "__"
            return filename.Substring(doubleUnderscoreIndex + 2);
        }

        // Return original filename if no "__" found
        return filename;
    }

    #endregion
}
