using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Database.PostgreSQL.Services;

public class PostgresWriteService : IDatabaseWriteService
{
    public async Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveLicenceAsync(string? licenceNumber, string licenceData, string? pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, string data)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SaveMatchesResultAsync(string matchesResult, string pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename, byte[] data,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string pageLines, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename, string noOcrServiceName,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber, int pageNumber,
        string extension, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task ClearCacheAsync()
    {
        throw new NotImplementedException();
    }

    public async Task ClearCacheAsync(string pdfFilename)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateProcessRunAsync(ProcessRun processRun)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence)
    {
        throw new NotImplementedException();
    }

    public async Task InsertLicenceSetLicenceAsync(int licenceSetId, int? licenceId, string? licenceNumber, string licenceVersionId,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetAggregateSetId, string serialize, int processRunId)
    {
        throw new NotImplementedException();
    }
}