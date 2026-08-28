using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Output.AbstractionLicence;

public class FileSystemAbstractionLicenceOutputService(string outputFolder) : IAbstractionLicenceOutputService
{
    public string? OutputFolder { get; set; } = outputFolder.StartsWith('/') ? outputFolder : Path.GetFullPath(outputFolder);
    
    public Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveLicenceSetsAsync(
        Dictionary<string, LicenceSet> licenceSets,
        Guid? fileId,
        int processRunId)
    {
        if (fileId == null)
        {
            return Task.CompletedTask;
        }
        
        var licenceSetsJson =
            Core.AbstractionLicence.Helpers.JsonHelper.GetAsString(licenceSets);
        
        return File.WriteAllTextAsync(
            $"{outputFolder}/{fileId}/licence-sets.jsonp",
            $"var licenceSets = {licenceSetsJson}");
    }

    public Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveLicenceAsync(Licence licence, int processRunId)
    {
        Directory.CreateDirectory($"{outputFolder}/{licence.DmsFileId}");
        
        var licenceJson = 
            WRADI.Core.AbstractionLicence.Helpers.JsonHelper.GetAsString(licence);

        await File.WriteAllTextAsync(
            $"{outputFolder}/{licence.DmsFileId}/licence.jsonp",
            $"var data2 = {licenceJson}");

        return -1;
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        var jsListFilePath = $"{outputFolder}list-data.js";

        return File.WriteAllTextAsync(jsListFilePath, "var data = " +
            JsonSerializer.Serialize(listData, JsonHelper.GetSerializerOptions()) + ";");
    }
    
    public Task FinishProcessRunAsync(ProcessRun processRun)
    {
        return Task.CompletedTask;
    }

    public Task UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        throw new NotImplementedException();
    }

    public Task UpdateLicenceListProcessRunAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> licences)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId, bool applyVerifications = false)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId, bool applyVerifications = false)
    {
        throw new NotImplementedException();
    }
    
    public Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        return Task.FromResult<IEnumerable<LicenceSectionVerification>>([]);
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId)
    {
        throw new NotImplementedException();
    }

    public
        Task<Dictionary<string, LicenceVerificationLookups>> GetVerificationLookupsBySectionNameAsync(int maxProcessRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        return Task.FromResult(0);
    }

    public Task<int> DeleteLicenceSectionVerificationAsync(int licenceSectionVerificationId)
    {
        return Task.FromResult(0);
    }

    public Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetDistinctIssuersAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetDistinctIssueDatesAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId)
    {
        throw new NotImplementedException();
    }
}