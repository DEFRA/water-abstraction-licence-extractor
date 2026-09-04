using System.Text;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Output.AbstractionLicence;

public class ApiAbstractionLicenceOutputService(HttpClient httpClient) : IAbstractionLicenceOutputService
{
    public string? OutputFolder { get; set; }
    
    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, Guid? fileId, int processRunId)
    {
        if (fileId == null)
        {
            return;
        }
        
        var path = "/Extractor/Licence/SaveLicenceSets";

        var json = JsonSerializer.Serialize(new
        {
            fileId,
            processRunId,
            licenceSets = JsonSerializer.Serialize(licenceSets, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId)
    {
        if (fileId == null)
        {
            return;
        }
        
        var path = "/Extractor/Licence/SaveLicenceSet";

        var json = JsonSerializer.Serialize(new
        {
            fileId,
            processRunId,
            licenceSet = JsonSerializer.Serialize(licenceSet, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveLicenceAsync(Licence licence, int processRunId)
    {
        var path = "/Extractor/Licence/Save";

        var json = JsonSerializer.Serialize(new
        {
            fileId = licence.DmsFileId,
            processRunId,
            licence = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public async Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId)
    {
        var path = "/Extractor/Licence/Update";

        var json = JsonSerializer.Serialize(new
        {
            licenceId,
            processRunId,
            licence = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        
        response.EnsureSuccessStatusCode();
    }
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun)
    {
        var path = "/Extractor/ProcessRun/Finish";

        var json = JsonSerializer.Serialize(new
        {
            processRun.ProcessRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        processRun.EndDateTimeUtc = DateTime.Parse(content);
    }

    public async Task UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers)
    {
        var path =
            $"/BFF/ProcessRuns/UpdateProcessRunByLicenceNumbers/{processRunId}";

        var json = JsonSerializer.Serialize(
            licenceNumbers,
            JsonHelper.GetSerializerOptions());

        using var httpContent = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await HttpHelper.RateLimiter.Enqueue(
            () => httpClient.PostAsync(
                new Uri(httpClient.BaseAddress!, path),
                httpContent));

        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateLicenceListProcessRunAsync(int processRunId)
    {
        var path = $"/BFF/ProcessRuns/UpdateLicenceListProcessRun/{processRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DocumentNaldPurposeMap>> GetDocumentNaldPurposeMapAsync()
    {
        var path = "/Extractor/NaldData/GetDocumentNaldPurposeMap";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DocumentNaldPurposeMap>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task AddDocumentNaldPurposeMapAsync(
        string documentDescription,
        NaldPurposeData naldPurpose,
        string matchType)
    {
        var path = "/Extractor/NaldData/AddDocumentNaldPurposeMap";

        var json = JsonSerializer.Serialize(
            new
            {
                DocumentDescription = documentDescription,
                NaldPurpose = naldPurpose,
                MatchType = matchType
            },
            JsonHelper.GetSerializerOptions());

        using var httpContent = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await HttpHelper.RateLimiter.Enqueue(
            () => httpClient.PostAsync(
                new Uri(httpClient.BaseAddress!, path),
                httpContent));

        response.EnsureSuccessStatusCode();
    }

    public async Task AddDocumentNaldPurposeMatchAsync(
        string licNo,
        string documentDescription,
        NaldPurposeData naldPurpose,
        string matchType)
    {
        var path = "/Extractor/NaldData/AddDocumentNaldPurposeMatch";

        var json = JsonSerializer.Serialize(
            new
            {
                LicNo = licNo,
                DocumentDescription = documentDescription,
                NaldPurpose = naldPurpose,
                MatchType = matchType
            },
            JsonHelper.GetSerializerOptions());

        using var httpContent = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        using var response = await HttpHelper.RateLimiter.Enqueue(
            () => httpClient.PostAsync(
                new Uri(httpClient.BaseAddress!, path),
                httpContent));

        response.EnsureSuccessStatusCode();
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        var path = $"/Extractor/Licence/GetAll?processRunId={processRunId}&skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Get all licences returned null");
        }

        return JsonSerializer.Deserialize<List<Licence>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        throw new NotImplementedException();
    }
    
    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(
        int processRunId,
        List<Licence> licences)
    {
        throw new NotImplementedException();
    }

    public async Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(
        int processRunId)
    {
        var path = $"/BFF/ProcessRuns/GetProcessRunLicenceSets?processRunId={processRunId}";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSets = JsonSerializer.Deserialize<Dictionary<string, LicenceSet>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSets == null)
        {
            throw new FileNotFoundException("Could not load licenceSets");
        }
        
        return licenceSets;
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public async Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId, bool applyVerifications = false)
    {
        var path = $"/Extractor/Licence/GetByFileId?fileId={fileId}&processRunId={processRunId}&applyVerifications={applyVerifications}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Licence>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId, bool applyVerifications = false)
    {
        var path = $"/Extractor/Licence/GetByLicenceNumber?licenceNumber={licenceNumber}&processRunId={processRunId}&applyVerifications={applyVerifications}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Licence>(content, JsonHelper.GetSerializerOptions())!;
    }

    public Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        var path = $"/BFF/FileData/LicenceSectionVerifications?licenceFileId={licenceFileId}";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSectionVerifications = JsonSerializer.Deserialize<IEnumerable<LicenceSectionVerification>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSectionVerifications == null)
        {
            throw new FileNotFoundException("Could not load licenceSectionVerifications");
        }
        
        return licenceSectionVerifications;
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetAllVerificationsAsync(int maxProcessRunId)
    {
        var path = $"/BFF/FileData/GetLatestLicenceSectionVerifications";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSectionVerifications = JsonSerializer.Deserialize<IEnumerable<LicenceSectionVerification>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSectionVerifications == null)
        {
            throw new FileNotFoundException("Could not load licenceSectionVerifications");
        }
        
        return licenceSectionVerifications;
    }

    public
        Task<Dictionary<string, LicenceVerificationLookups>> GetVerificationLookupsBySectionNameAsync(int maxProcessRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        throw new NotImplementedException();
    }

    public Task<int> DeleteLicenceSectionVerificationAsync(int licenceSectionVerificationId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        var path = $"/BFF/ProcessRuns/GetTotalLicenceCount?processRunId={processRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
            
        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Total licence count returned null");
        }

        return JsonSerializer.Deserialize<int>(content, JsonHelper.GetSerializerOptions())!;
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