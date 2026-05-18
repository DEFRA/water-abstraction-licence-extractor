namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfImageService
{
    public Task<(string Extension, int ImageNumber)> SaveImageBytesAsync(
        Guid fileId,
        int imageNumber,
        int pageNumber,
        ICacheService cacheService,
        int processRunId);
}