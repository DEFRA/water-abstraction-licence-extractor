namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfImageService
{
    public Task<string?> SaveImageBytesAsync(Guid fileId, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId);
}