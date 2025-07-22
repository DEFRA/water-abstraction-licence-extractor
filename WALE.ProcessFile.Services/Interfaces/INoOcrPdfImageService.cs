namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfImageService
{
    string GetFilepath(int imageNumber, int pageNumber, string outputFolder, bool createDirectory, string extension);
    
    public Task<string> SaveImageBytesAsync(int imageNumber, int pageNumber, string outputFolder);
}