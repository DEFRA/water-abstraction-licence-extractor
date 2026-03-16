namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfImageService
{
    public Task<string?> SaveImageBytesAsync(string filename, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId);
}