using WALE.Tools._1stHalf;
using WALE.Tools._2ndHalf;
using WALE.Tools._2ndHalf.ImportNaldData;
using WALE.Tools.Config;

string workflow;
//workflow = "FilesAvailableForLicenceIdentificationExtract";
workflow = "ImportNaldData";
//workflow = "ImportDmsData";
//workflow = "RemoveRedundantFilesFromS3";
//workflow = "ClearCacheMultiple";
//workflow = "GenerateLicenceReaderExtract";
//workflow = "ImportOverrideData";
//workflow = "CopyS3Files";
//workflow = "ForceLowercaseS3Files";
//workflow = "GenerateLinkedLicencesCsv";
workflow = "PurposeMapper";

const int processRunId = 3296;//112;//1707;
var localPdfFolder = KeyConfig.PdfFolder5; //KeyConfig.PdfFolderForDuplicates; //KeyConfig.PdfFolder5;
var duplicateResultsFilePath = Path.Combine(KeyConfig.PdfFolderForDuplicates, "Download_Info_20260218-2.xlsx"); // File comes from JP
var folderPathUsername = "xxx";

switch (workflow)
{
    case "ImportNaldData": // FREQUENT - Import needed to import NALD data from CSVs (from FME S3) into the DB
        return await ImportNaldData.ImportAsync();
    
    case "ImportDmsData": // FREQUENT - Import needed to import DMS data from XLSX file (local fs) into the DB
        return await ImportDmsData.ImportAsync();
    
    case "ImportOverrideData": // FREQUENT - Import needed to import override data from XLSX file (local fs) into the DB
        throw new NotImplementedException(); // TODO
 
    case "GenerateLicenceReaderExtract": // FREQUENT - Foreach file in storage, scrape (and feed into the DB);
        // - DOI (that will be used in Live Licence Identification)
        // - Licence number
        // - Which template does it match
        // - Fetch number of pages
        // Addendum / schedule

        var includeVersionMatch = false;
        return await GenerateLicenceReaderExtract.GenerateLicenceReaderExtractAsync(includeVersionMatch);
    
    case "DuplicateLicenceIdentificationExtractBySize": // INFREQUENT - Identify duplicates by file size
        return await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(
            duplicateResultsFilePath,
            localPdfFolder,
            false);
    
    case "GenerateAggregatesCsvForTesting": // INFREQUENT - A file to give to James and team
        return await GenerateAggregatesCsvForTesting.GenerateCsvForTestingAsync(processRunId);
    
    case "GenerateLinkedLicencesCsv": // NOT ENVISIONED TO BE USED ANY LONGER - Generates a linked licence
        // file for Mitin and Shaun
        await GenerateLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    
    case "FilesAvailableForLicenceIdentificationExtract": // SUPERSEDED BY API / THIS PRODUCES REPORT
        // Identify all filenames and metadata (s3/local) that we  have stored (feeds into other process). May
        // be useful to find deltas of downloaded files
        
        await InventoryFileGenerator.GenerateWaterPdfsFolderInventoryAsync(folderPathUsername);
        break;
    
    case "DuplicateLicenceIdentificationExtract": // OBSOLETE - Identify duplicates by name
        await DuplicateLicenceIdentificationExtract.GenerateDuplicateLicenceIdentificationExtractAsync(
            duplicateResultsFilePath,
            KeyConfig.PdfFolderForDuplicates,
            true);
        break;
    
    case "RemoveRedundantFilesFromS3": // ONE-OFF - Remove duplicate and files with incorrect names in S3
        await RemoveRedundantFilesFromS3.RunAsync();
        break;
    
    case "ClearCacheMultiple": // ONE-OFF - Clear multiple caches
        await ClearCacheMultiple.RunAsync();
        break;
    
    case "GenerateEALicenceFeaturesCsv": // ONE-OFF - Pull licence features out a file
        await GenerateEaLicenceFeaturesCsv.GenerateCsvAsync(processRunId);
        break;
    
    case "PopulateCachedImageWidthAndHeights": // ONE-OFF - Populate image widths and heights for cached images
        await PopulateCachedImageWidthAndHeights.PopulateWidthAndHeightsAsync();
        break;
    
    case "UpdateCachedImageWidthAndHeightsFilenames": // ONE-OFF - Populate image widths and heights for cached images
        await UpdateCachedImageWidthAndHeightsFilenames.PopulateWidthAndHeightsAsync();
        break;
    
    case "GenerateUnknownSectionLinkedLicencesCsv": // ONE-OFF - A debugging file
        await GenerateUnknownSectionLinkedLicencesCsv.GenerateCsvAsync(processRunId);
        break;
    
    case "OverrideAddIncrements": // ONE OFF - Add increment info to override file
        var overrideRootPath = $"/Users/{folderPathUsername}/Documents/GitHub/water-abstraction-licence-finder/WA.DMS.LicenceFinder.Services/Resources";
        await OverrideAddIncrements.GenerateOverrideFileAsync(overrideRootPath);
        break;
    
    case "TestsForAiPrompts": // POC - An old POC in AI prompts to read files
        await TestsForAiPrompts.TestsForAiPromptsAsync();
        break;
    
    case "CopyS3Files": // UNCOMMONLY USED - Promotion of S3 files between environments
        await CopyS3Files.RunAsync();
        break;
    
    case "ForceLowercaseS3Files": // UNCOMMONLY USED - Fix casing of S3 files
        await ForceLowercaseS3Files.RunAsync();
        break;
    case "PurposeMapper":
        await PurposeMapperSinglePurpose.RunAsync(processRunId);
        
        //PurposeMapperLlm.MapPurposes(
         //   "/Users/ryanbarlow/Documents/NaldPurposes.csv",
         //   "/Users/ryanbarlow/Documents/DocumentPurposes.txt");
        break;
}

return 0;