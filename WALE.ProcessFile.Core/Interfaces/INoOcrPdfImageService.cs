namespace WALE.ProcessFile.Core.Interfaces;

public interface INoOcrPdfImageService
{
    //string GetImageFilepath(int imageNumber, int pageNumber, ICacheService cacheService, bool createDirectory, string extension);
    
    public Task<string?> SaveImageBytesAsync(string filePath, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId);
}