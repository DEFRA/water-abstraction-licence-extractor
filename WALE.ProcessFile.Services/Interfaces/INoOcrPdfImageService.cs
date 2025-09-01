namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfImageService
{
    string GetImageFilepath(int imageNumber, int pageNumber, string cacheFolder, bool createDirectory, string extension);
    
    public Task<string?> SaveImageBytesAsync(int imageNumber, int pageNumber, string cacheFolder);
}