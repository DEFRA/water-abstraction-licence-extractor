using DocumentFormat.OpenXml.Office2010.ExcelAc;
using WALE.ProcessFile.Services.Cache;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

public class OverrideAddIncrements
{
    public static async Task GenerateOverrideFileAsync(string rootPath)
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);
    
        var cacheService = new ApiCacheService(httpClient);
        var fileProcessor = new LicenceFileProcessor();
        
        var oldFiles = GetDmsChangeAuditOverrides(rootPath, fileProcessor);

        foreach (var oldFile in oldFiles)
        {
            var newFormatOverrides = new List<OverrideNewFormat>();
            
            foreach (var overrideOldFormat in oldFile.Value)
            {
                var issueNumber = int.Parse(overrideOldFormat.IssueNo);
                
                var incrementNumber = await cacheService.GetNaldLicenceIncrementNumberAsync(
                    overrideOldFormat.PermitNumber,
                    issueNumber);
                
                newFormatOverrides.Add(new OverrideNewFormat
                {
                    PermitNumber = overrideOldFormat.PermitNumber,
                    IssueNo = overrideOldFormat.IssueNo,
                    FileId = overrideOldFormat.FileId,
                    FileUrl = overrideOldFormat.FileUrl,
                    IncrementNo = incrementNumber
                });
            }
            
            fileProcessor.GenerateExcel(
                newFormatOverrides,
                oldFile.Key.Replace(".xlsx", $"_{DateTime.Now:yyyyMMdd}.xlsx", StringComparison.InvariantCultureIgnoreCase),
                new Dictionary<string, string>
                {
                    { "PermitNumber", "Permit Number"},
                    { "FileUrl", "File URL"},
                    { "FileId", "File ID"},
                    { "IssueNo", "NALD Issue No."},
                    { "IncrementNo", "NALD Increment No."}
                });
        }
    }
    
    private static Dictionary<string, List<OverrideOldFormat>> GetDmsChangeAuditOverrides(
        string rootPath,
        LicenceFileProcessor fileProcessor)
    {
        var allOverrides = new Dictionary<string, List<OverrideOldFormat>>();
        
        var overrides = new List<string>
        {
            $"{rootPath}/Overrides.xlsx",
            $"{rootPath}/ANGLIAN_Overrides.xlsx"
        };

        if (!overrides.Any())
        {
            throw new FileNotFoundException("No override files were found.");
        }
        
        foreach (var overrideFilePath in overrides)
        {
            var records = fileProcessor.ExtractExcel<List<OverrideOldFormat>>(
                overrideFilePath,
                new Dictionary<string, List<string>>
                {
                    { "Permit Number", ["PermitNumber"]},
                    { "File URL", ["FileUrl"]},
                    { "NALD Issue_No", ["IssueNo"]},
                    { "NALD Issue No.", ["IssueNo"]},
                    { "File ID", ["FileId"]}
                });

            allOverrides.Add(overrideFilePath, records);
        }

        return allOverrides;
    }
}