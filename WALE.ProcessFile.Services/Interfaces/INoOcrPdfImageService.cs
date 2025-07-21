namespace WALE.ProcessFile.Services.Interfaces;

public interface INoOcrPdfImageService
{
    public Task<byte[]> GetImageBytesAsync(int imageNumber, int pageNumber, string outputFolder);

    string GetFilepath(int imageNumber, int pageNumber, string outputFolder, bool createDirectory);
    
    public Task SaveImageBytesAsync(int imageNumber, int pageNumber, string outputFolder);
}