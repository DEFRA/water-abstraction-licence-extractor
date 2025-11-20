namespace WALE.ProcessFile.Models.Interfaces;

public interface INoOcrPdfImageService
{
    //string GetImageFilepath(int imageNumber, int pageNumber, ICacheService cacheService, bool createDirectory, string extension);
    
    public Task<string?> SaveImageBytesAsync(string folderPath, int imageNumber, int pageNumber, ICacheService cacheService, int processRunId);
}