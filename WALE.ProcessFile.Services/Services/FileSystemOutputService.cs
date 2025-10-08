using System.Text.Json;
using WALE.ProcessFile.Models.Database;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Services;

public class FileSystemOutputService(string outputFolder) : IOutputService
{
    public Task SetupAsync()
    {
        Directory.CreateDirectory(outputFolder);
        return Task.CompletedTask;
    }
    
    public Task<ProcessRun> RecordProcessRunStartAsync()
    {
        // This mode doesn't need to do anything here
        return Task.FromResult(new ProcessRun());
    }

    public Task SaveLicenceSetsAsync(
        IReadOnlyList<LicenceSet> licenceSets,
        string pdfFilePath)
    {
        var licenceSetsJson = JsonHelper.GetAsString(licenceSets);
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");
    }

    public Task SaveLicenceAsync(Licence licence, string pdfFilePath)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");
        
        var licenceJson = JsonHelper.GetAsString(licence);

        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/licence.jsonp",
            $"var data2 = {licenceJson}");
    }
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath)
    {
        var folderName = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        Directory.CreateDirectory($"{outputFolder}/{folderName}");

        var internalJson = JsonHelper.GetAsString(matchesResult);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{folderName}/internal.jsonp",
            $"var data = {internalJson}");
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData)
    {
        var jsListFilePath = $"{outputFolder}list-data.js";

        return File.WriteAllTextAsync(jsListFilePath, "var data = " +
            JsonSerializer.Serialize(listData, JsonHelper.GetSerializerOptions()) + ";");
    }
}