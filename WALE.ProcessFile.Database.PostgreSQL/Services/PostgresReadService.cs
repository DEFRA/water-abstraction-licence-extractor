using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresReadService : IDatabaseReadService
{
    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(string filename, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<AggregateSet[]?> GetAggregateSets(int licenceSetId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<Licence?> GetLicenceAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public async Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<MatchesResult?> GetMatchesResult(string filename)
    {
        throw new NotImplementedException();
    }
}